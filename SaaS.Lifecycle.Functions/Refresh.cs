using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SaaS.Lifecycle.Functions.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SaaS.Lifecycle.Functions
{
    public static class Refresh
    {
        [FunctionName("Refresh")]
        public static async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer, ILogger log)
        {
            const int apiPageSize = 100;

            const string apiEtagHeaderName = "ETag";
            const string apiEtagMetadataName = "gh_api_etag";
            const string mapBlobName = "repo_map.json";

            try
            {
                // Grab the settings we're going to need to do this...

                var pat = Environment.GetEnvironmentVariable("GitHubPat");
                var storageConnString = Environment.GetEnvironmentVariable("StorageConnectionString");
                var containerName = Environment.GetEnvironmentVariable("RepoMapStorageContainerName");
                var repoOwner = Environment.GetEnvironmentVariable("RepoOwnerName");

                // Try to pull an existing repo map from blob storage...

                string apiEtag = null;

                var serviceClient = new BlobServiceClient(storageConnString);
                var containerClient = serviceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(mapBlobName);

                if (await blobClient.ExistsAsync())
                {
                    log.LogDebug($"Loading cached repo map from [{containerName}/{mapBlobName}]...");

                    var blobProperties = (await blobClient.GetPropertiesAsync()).Value;

                    if (blobProperties.Metadata.ContainsKey(apiEtagMetadataName))
                    {
                        apiEtag = blobProperties.Metadata[apiEtagMetadataName];

                        log.LogDebug($"Cached repo map [{containerName}/{mapBlobName}] GitHub API ETag is [{apiEtag}].");
                    }
                    else
                    {
                        log.LogWarning($"Cached repo map [{containerName}/{mapBlobName}] has no associated GitHub API ETag.");
                    }
                }
                else
                {
                    log.LogWarning("Cached repo map not found.");
                }

                var repoMap = new List<CandidateRepo>();

                log.LogDebug("Refreshing repo map...");

                using (var httpClient = new HttpClient { BaseAddress = new Uri("https://api.github.com") })
                {
                    httpClient.DefaultRequestHeaders.Clear();
                    httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.mercy-preview+json");
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"token {pat}");

                    if (!string.IsNullOrEmpty(apiEtag))
                    {
                        httpClient.DefaultRequestHeaders.Add("If-None-Match", apiEtag);
                    }

                    var jsonSerializer = new JsonSerializer();

                    // Page through all the repos and grab the ones that we care about...

                    for (int pageIndex = 1; ; pageIndex++)
                    {
                        log.LogDebug($"Getting page [{pageIndex}] of [{repoOwner}]'s repos from GitHub API...");

                        // TODO: Sprinkle in some Polly.

                        var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/users/{repoOwner}/repos?page={pageIndex}&per_page={apiPageSize}");
                        var httpResponse = await httpClient.SendAsync(httpRequest);

                        if (httpResponse.StatusCode == System.Net.HttpStatusCode.NotModified)
                        {
                            // No changes! This is a good thing. 304s don't impact our rate limit...

                            log.LogDebug("No repo updates (304 Not Modified). No need to refresh repo map.");

                            return;
                        }

                        // If it's anything else, we should just stop and try again in five minutes...

                        httpResponse.EnsureSuccessStatusCode();

                        if (pageIndex == 1 && httpResponse.Headers.Contains(apiEtagHeaderName))
                        {
                            // Try to get ETag from first page...

                            // Apparently, you can use the ETag from the first page of GitHub results to determine if
                            // the entire result set (all the pages) has changed.

                            apiEtag = httpResponse.Headers.GetValues(apiEtagHeaderName).First();

                            log.LogDebug($"Updated repo map GitHub API ETag is [{apiEtag}].");
                        }

                        var httpContent = await httpResponse.Content.ReadAsStringAsync();
                        var pageRepos = JsonConvert.DeserializeObject<List<Repo>>(httpContent);

                        // Pick out the repos that we care about and parse their labels...

                        var pageCandidateRepos = pageRepos
                            .Where(r => r.IsPotentialCandidate())
                            .Select(r => new CandidateRepo(r)).ToList();

                        if (pageCandidateRepos.Any())
                        {
                            log.LogDebug($"Adding [{pageCandidateRepos.Count}] candidate repo(s) from page [{pageIndex}] to repo map...");

                            repoMap.AddRange(pageCandidateRepos);
                        }
                        else
                        {
                            log.LogDebug($"No candidate repos found on page [{pageIndex}].");
                        }

                        if (pageRepos.Count < apiPageSize) break; // We've reached the end of the list.
                    }
                }

                // All done! Assuming we got this far (we actually have updates), refresh the repo map in blob storage and update the
                // GitHub API ETag. See you again in five minutes...

                await blobClient.UploadAsync(new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(repoMap))), overwrite: true);
                await blobClient.SetMetadataAsync(new Dictionary<string, string> { [apiEtagMetadataName] = apiEtag });
            }
            catch (Exception ex)
            {
                // Bummer! Something broke. Since this function runs every five minutes and our only source of truth is the repo map blob,
                // we shouldn't need to do any kind of cleanup at this point. Chances are, the next run will take care of it. If this is an
                // an ongoing issue, we should already know about it through the logs + alert rules...

                log.LogError(ex, "An error occurred while attemping to refresh the repo map. See exception for details.");

                throw;
            }
        }
    }
}
