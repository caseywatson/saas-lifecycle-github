using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SaaS.Lifecycle.Functions.Models;
using System.Linq;
using System.Collections.Generic;
using Azure.Storage.Blobs;
using System.Text;

namespace SaaS.Lifecycle.Functions
{
    public static class RunFunction
    {
        [FunctionName("Run")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "/saas/tenants/{tenantId:alpha}/subscriptions/{subscriptionId:alpha}/{actionType:alpha}")] HttpRequest req,
            ILogger log, string tenantId, string subscriptionId, string actionType)
        {
            const string mapBlobName = "repo_map.json";

            var httpContent = await new StreamReader(req.Body).ReadToEndAsync();
            var opRequest = JsonConvert.DeserializeObject<OperationRequest>(httpContent);

            if (opRequest.Selectors?.Any() != true)
            {
                // We need at least one selector...

                return new BadRequestObjectResult("At least one selector is required.");
            }

            // Grab the settings that we're going to need to do this...

            var pat = Environment.GetEnvironmentVariable("GitHubPat");
            var storageConnString = Environment.GetEnvironmentVariable("StorageConnectionString");
            var opContainerName = Environment.GetEnvironmentVariable("OperationStorageContainerName");
            var repoMapContainerName = Environment.GetEnvironmentVariable("RepoMapStorageContainerName");
            var repoOwner = Environment.GetEnvironmentVariable("RepoOwnerName");

            List<CandidateRepo> repoMap = null;

            var serviceClient = new BlobServiceClient(storageConnString);
            var containerClient = serviceClient.GetBlobContainerClient(repoMapContainerName);
            var repoMapBlobClient = containerClient.GetBlobClient(mapBlobName);

            if (await repoMapBlobClient.ExistsAsync() == false)
            {
                // We don't even have a repo map yet. We can't do anything. Sorry, we're closed!

                return new StatusCodeResult(503); // Service unavailable.
            }

            var repoMapBlobContent = await repoMapBlobClient.DownloadContentAsync();
            var repoMapBlobString = Encoding.UTF8.GetString(repoMapBlobContent.Value.Content);

            repoMap = JsonConvert.DeserializeObject<List<CandidateRepo>>(repoMapBlobString);

            // Alright, let's find our repo...

            var selectedRepos = SelectCandidateRepos(repoMap, opRequest);

            if (!selectedRepos.Any())
            {
                // We can't find a repo to handle this operation...

                return new NotFoundObjectResult("No matching workflow found.");
            }

            if (selectedRepos.Count > 1)
            {
                // We can't decide which repo to use...

                return new ConflictObjectResult($"Can't decide which workflow to run — {string.Join(" or ", selectedRepos.Select(i => $"[{i.Repo.Name}]"))}.");
            }

            return new OkResult();
        }

        private static IList<CandidateRepo> SelectCandidateRepos(List<CandidateRepo> repoMap, OperationRequest opRequest) =>
            repoMap.Where(rm => opRequest.Selectors.All(s => rm.Labels.Contains(s))).ToList();
    }
}
