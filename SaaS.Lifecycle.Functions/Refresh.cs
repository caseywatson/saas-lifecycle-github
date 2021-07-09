using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using SaaS.Lifecycle.Functions.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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

            var pat = Environment.GetEnvironmentVariable("GitHubPat");
            var storageConnString = Environment.GetEnvironmentVariable("StorageConnectionString");
            var containerName = Environment.GetEnvironmentVariable("RepoMapStorageContainerName");
            var repoOwner = Environment.GetEnvironmentVariable("RepoOwnerName");

            // Decide whether or not we need to refresh our repo map by checking to see whether the 
            // repo map is present or the etag associated with the repo map is stale...

            string apiEtag = null;

            var serviceClient = new BlobServiceClient(storageConnString);
            var containerClient = serviceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(mapBlobName);

            if (await blobClient.ExistsAsync())
            {
                var blobProperties = (await blobClient.GetPropertiesAsync()).Value;

                if (blobProperties.Metadata.ContainsKey(apiEtagMetadataName))
                {
                    apiEtag = blobProperties.Metadata[apiEtagMetadataName];
                }
            }

            // See if we have an updated repo list to load...

            var repos = new List<Repo>();

            using (var httpClient = new HttpClient { BaseAddress = new Uri("https://api.github.com") })
            {
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.mercy-preview+json");
                httpClient.DefaultRequestHeaders.Add("Authorization", $"token {pat}");

                if (!string.IsNullOrEmpty(apiEtag))
                {
                    // If we have an etag, use it...

                    httpClient.DefaultRequestHeaders.Add("If-None-Match", apiEtag);
                }

                for (int pageIndex = 1; ; pageIndex++)
                {
                    var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/users/{repoOwner}/repos?page={pageIndex}&per_page={apiPageSize}");
                    var httpResponse = await httpClient.SendAsync(httpRequest);

                    // Nothing's been modified. We're done here...

                    if (httpResponse.StatusCode == System.Net.HttpStatusCode.NotModified) return;

                    httpResponse.EnsureSuccessStatusCode();

                    if (pageIndex == 1 && httpResponse.Headers.Contains(apiEtagHeaderName))
                    {
                        apiEtag = httpResponse.Headers.GetValues(apiEtagHeaderName).First();
                    }

                    using (var responseStream = await httpResponse.Content.ReadAsStreamAsync())
                    {
                        var pageRepos = await JsonSerializer.DeserializeAsync<IList<Repo>>(responseStream);

                        repos.AddRange(pageRepos);

                        // Check to see if this is the end of the list...

                        if (pageRepos.Count < apiPageSize) break;
                    }
                }
            }

            await blobClient.UploadAsync(new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(repos))), overwrite: true);
            await blobClient.SetMetadataAsync(new Dictionary<string, string> { [apiEtagMetadataName] = apiEtag });
        }
    }
}
