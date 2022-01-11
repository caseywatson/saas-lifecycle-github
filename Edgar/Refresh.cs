using Azure.Storage.Blobs;
using Edgar.Functions.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Edgar.Functions
{
    public static class Refresh
    {
        public const string ApiEtagBlobMetadataName = "gh_api_etag";
        public const string ApiEtagResponseHeaderName = "ETag";

        [FunctionName("Refresh")]
        public static async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer, ILogger log)
        {
            const int apiPageSize = 100;
            const string mapBlobName = "repo_map.json";

            try
            {
                // Grab the settings we're going to need to do this...

                var pat = Environment.GetEnvironmentVariable("GitHubPat");
                var storageConnString = Environment.GetEnvironmentVariable("StorageConnectionString");
                var containerName = Environment.GetEnvironmentVariable("RepoMapStorageContainerName");
                var repoOwner = Environment.GetEnvironmentVariable("RepoOwnerName");

                // Try to pull an existing repo map from blob storage...

                var serviceClient = new BlobServiceClient(storageConnString);
                var containerClient = serviceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(mapBlobName);

                // Try to get our current repo map's etag. This etag is used by GitHub to cache API responses
                // when the list of repos hasn't changed. We try our best to be responsible API consumers. ;)
                // For more info, see https://docs.github.com/en/rest/overview/resources-in-the-rest-api#conditional-requests.

                var apiEtag = await blobClient.TryGetCurrentRepoMapEtagAsync();

                log.LogDebug("Refreshing repo map...");

                var repoMap = new List<Repo>();
                var jsonSerializer = new JsonSerializer();
                var ghHttpClient = CreateGitHubHttpClient(pat, apiEtag);

                // Page through all the repos and grab the ones that we care about...

                for (int pageIndex = 1; ; pageIndex++)
                {
                    log.LogDebug($"Getting page [{pageIndex}] of [{repoOwner}]'s repos from GitHub API...");

                    // TODO: Sprinkle in some Polly.

                    var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/user/repos?page={pageIndex}&per_page={apiPageSize}");
                    var httpResponse = await ghHttpClient.SendAsync(httpRequest);

                    if (httpResponse.StatusCode == System.Net.HttpStatusCode.NotModified)
                    {
                        // No changes! This is a good thing. 304s don't impact our rate limit...

                        log.LogInformation("No repo updates (304 Not Modified). Repo map refresh unnecessary.");

                        return;
                    }

                    // If it's anything else, we should just stop and try again in five minutes...

                    httpResponse.EnsureSuccessStatusCode();

                    if (pageIndex == 1)
                    {
                        apiEtag = httpResponse.TryGetGitHubResponseEtag();
                    }

                    var httpContent = await httpResponse.Content.ReadAsStringAsync();
                    var pageRepos = JsonConvert.DeserializeObject<List<Repo>>(httpContent);

                    // Pick out the repos that we care about and parse their labels...

                    var pageCandidateRepos = pageRepos.Where(r => r.IsPotentialCandidate()).ToList();

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

                // All done! Assuming we got this far (we actually have updates), refresh the repo map in blob storage and update the
                // GitHub API ETag. See you again in five minutes...

                await blobClient.UpdateRepoMapAsync(repoMap, apiEtag);

                log.LogInformation($"Repo map updated. Map now contains [{repoMap.Count}] candidate repos. Updated GitHub API ETag is [{apiEtag}].");
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

        private static async Task UpdateRepoMapAsync(this BlobClient blobClient, List<Repo> repoMap, string apiEtag)
        {
            await blobClient.UploadAsync(new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(repoMap))), overwrite: true);
            await blobClient.SetMetadataAsync(new Dictionary<string, string> { [ApiEtagBlobMetadataName] = apiEtag });
        }

        private static string TryGetGitHubResponseEtag(this HttpResponseMessage httpResponse)
        {
            if (httpResponse.Headers.Contains(ApiEtagResponseHeaderName))
            {
                // Try to get ETag from first page...

                // Apparently, you can use the ETag from the first page of GitHub results to determine if
                // the entire result set (all the pages) has changed.

                return httpResponse.Headers.GetValues(ApiEtagResponseHeaderName).First().Trim('"'); // What? Really? Why would I want the quotation marks?
            }
            else
            {
                return null;
            }
        }

        private async static Task<string> TryGetCurrentRepoMapEtagAsync(this BlobClient blobClient)
        {
            if (await blobClient.ExistsAsync())
            {
                var blobProperties = (await blobClient.GetPropertiesAsync()).Value;

                if (blobProperties.Metadata.ContainsKey(ApiEtagBlobMetadataName))
                {
                    return blobProperties.Metadata[ApiEtagBlobMetadataName];
                }
            }

            return null;
        }

        private static HttpClient CreateGitHubHttpClient(string pat, string apiEtag = null)
        {
            var httpClient = new HttpClient { BaseAddress = new Uri("https://api.github.com") };

            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            httpClient.DefaultRequestHeaders.Add("Authorization", $"token {pat}");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "SaaS-Lifecycle");

            if (!string.IsNullOrEmpty(apiEtag))
            {
                httpClient.DefaultRequestHeaders.Add("If-None-Match", apiEtag);
            }

            return httpClient;
        }
    }
}
