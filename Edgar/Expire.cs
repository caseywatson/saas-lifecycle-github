using Azure;
using Azure.Messaging.EventGrid;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Edgar.Functions.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Edgar.Functions
{
    public static class Expire
    {
        [FunctionName("Expire")]
        public async static Task Run(
            [TimerTrigger("0 0 0 * * *")] TimerInfo myTimer,
            [EventGrid(TopicEndpointUri = "EventGridEndpoint", TopicKeySetting = "EventGridKey")] IAsyncCollector<EventGridEvent> eventCollector,
            ILogger log)
        {
            try
            {
                var utcNow = DateTime.UtcNow;

                // Grab the settings we're going to need to do this...

                var storageConnString = Environment.GetEnvironmentVariable("StorageConnectionString");
                var containerName = Environment.GetEnvironmentVariable("OperationStorageContainerName");

                var expirationInHours = GetOperationExpirationInHours();
                var expirationDateTime = utcNow.AddHours(Math.Abs(expirationInHours) * -1);

                log.LogInformation($"It is now [{utcNow}] UTC. Expiring operations created on/before [{expirationDateTime} ({expirationInHours} hour(s) ago)]...");

                // Let's go see what operations are pending...

                var opBlobs = new List<BlobItem>();
                var serviceClient = new BlobServiceClient(storageConnString);
                var containerClient = serviceClient.GetBlobContainerClient(containerName);
                var opBlobPages = containerClient.GetBlobsAsync().AsPages();

                await foreach (Page<BlobItem> opBlobPage in opBlobPages)
                {
                    foreach (var expiredOpBlob in opBlobPage.Values.Where(b => b.Properties.CreatedOn != null && b.Properties.CreatedOn.Value <= expirationDateTime))
                    {
                        try
                        {
                            var opBlobClient = containerClient.GetBlobClient(expiredOpBlob.Name);
                            var operation = await opBlobClient.GetOperationAsync();

                            log.LogWarning($"Old operation [{operation.OperationId}] has expired. Deleting operation blob...");

                            await opBlobClient.DeleteAsync();
                            await eventCollector.AddAsync(ToOperationTimedOutEvent(operation));
                        }
                        catch (Exception ex)
                        {
                            // It's possible, although unlikely, that we could run into a "poison operation" situation where a single expired blob
                            // could potentially block other operations from being expired. For that reason, we have an inner try/catch loop so that,
                            // even if one operation blob gives us problems, we can move onto the next one.

                            log.LogError(ex, $"An error occurred while attempting to expire old operation blob [{expiredOpBlob.Name}]. Please review exception for details.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, $"An error occurred while attempting to expire old operations. Please review exception for details.");
            }
        }

        private static async Task<Models.Operation> GetOperationAsync(this BlobClient opBlobClient)
        {
            var blobContent = await opBlobClient.DownloadContentAsync();
            var blobString = Encoding.UTF8.GetString(blobContent.Value.Content.ToArray());

            return JsonConvert.DeserializeObject<Models.Operation>(blobString);
        }

        private static int GetOperationExpirationInHours()
        {
            const int defaultExpiration = 48;

            if (int.TryParse(Environment.GetEnvironmentVariable("OperationExpirationInHours"), out var expiration))
            {
                return expiration;
            }
            else
            {
                return defaultExpiration;
            }
        }

        private static EventGridEvent ToOperationTimedOutEvent(Models.Operation operation) =>
            new EventGridEvent(
                $"/saas/tenants/{operation.TenantId}/subscriptions/{operation.SubscriptionId}",
                EventTypeNames.SubscriptionConfigurationTimedOut,
                OperationEvent.DataVersion,
                new OperationEvent(operation))
            {
                EventTime = DateTime.UtcNow,
                Id = Guid.NewGuid().ToString()
            };
    }
}
