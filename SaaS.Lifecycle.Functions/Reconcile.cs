using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using SaaS.Lifecycle.Functions.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SaaS.Lifecycle.Functions
{
    public static class Reconcile
    {
        [FunctionName("Reconcile")]
        public static async Task Run([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, ILogger log)
        {
            try
            {
                // Grab the settings we're going to need to do this...

                var pat = Environment.GetEnvironmentVariable("GitHubPat");
                var storageConnString = Environment.GetEnvironmentVariable("StorageConnectionString");
                var containerName = Environment.GetEnvironmentVariable("OperationStorageContainerName");
                var repoOwner = Environment.GetEnvironmentVariable("repoOwner");

                // Let's go see what operations are pending...

                var serviceClient = new BlobServiceClient(storageConnString);
                var containerClient = serviceClient.GetBlobContainerClient(containerName);
                var opBlobs = new List<BlobItem>();
                var opBlobPages = containerClient.GetBlobsAsync().AsPages();

                await foreach (Page<BlobItem> opBlobPage in opBlobPages)
                {
                    // Presumably, these are all pending operations...

                    opBlobs.AddRange(opBlobPage.Values);
                }

                var opBlobsByRepo = (from opBlob in opBlobs
                                     let nameParts = opBlob.Name.Trim().Split('/') // Blob name should be [repo-name/run-id]...
                                     where (nameParts.Length == 2) && nameParts.All(np => !string.IsNullOrEmpty(np)) // Make sure that both parts are there, then...
                                     select new { RepoName = nameParts[0], RunId = nameParts[1], Blob = opBlob }) // Get the repo name, run ID, and the blob itself, then...                                                                              
                                     .GroupBy(g => g.RepoName) // Group by repo name as this is how we'll work them, then...
                                     .ToDictionary(d => d.Key, d => d.ToDictionary(a => a.RunId, a => a.Blob)); // Collapse it all down into a nested dictionary we can work with.

                if (opBlobsByRepo.Any())
                {
                    using (var httpClient = new HttpClient { BaseAddress = new Uri("https://api.github.com") })
                    {
                        httpClient.DefaultRequestHeaders.Clear();
                        httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
                        httpClient.DefaultRequestHeaders.Add("Authorization", $"token {pat}");

                        // For each applicable repo...

                        foreach (var repoName in opBlobsByRepo.Keys)
                        {
                            // Let's get the latest completed workflow runs for this repo...

                            var httpRequest = new HttpRequestMessage(HttpMethod.Get,
                                $"/repos/{repoOwner}/{repoName}/actions/runs?event=workflow_dispatch&status=completed&per_page=100");

                            var httpResponse = await httpClient.SendAsync(httpRequest);

                            if (httpResponse.IsSuccessStatusCode)
                            {
                                using (var responseStream = await httpResponse.Content.ReadAsStreamAsync())
                                {
                                    // Get the repo runs...

                                    var repoRuns = await JsonSerializer.DeserializeAsync<RepoRuns>(responseStream);

                                    if (repoRuns.Runs.Any())
                                    {
                                        // Try to match each operation run ID to a GitHub run ID...

                                        foreach (var opRunId in opBlobsByRepo[repoName].Keys)
                                        {
                                            var repoRun = repoRuns.Runs.FirstOrDefault(r => r.Id == opRunId);

                                            if (repoRun != null)
                                            {
                                                // Score! We found a match!

                                                await ReconcileOperation(opBlobsByRepo[repoName][opRunId], repoRun);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
        }
        
        private static async Task ReconcileOperation(BlobItem opBlob, Run ghRun)
        {

        }
    }
}
