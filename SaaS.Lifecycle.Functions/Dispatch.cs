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
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Azure.EventGrid.Models;
using System.Net;

namespace SaaS.Lifecycle.Functions
{
    public static class Dispatch
    {
        [FunctionName("Dispatch")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "saas/tenants/{tenantId}/subscriptions/{subscriptionId}/{actionType}")] HttpRequest req,
            [EventGrid(TopicEndpointUri = "EventGridEndpoint", TopicKeySetting = "EventGridKey")] IAsyncCollector<EventGridEvent> eventCollector,
            ILogger log, string tenantId, string subscriptionId, string actionType)
        {
            var httpContent = await new StreamReader(req.Body).ReadToEndAsync();
            var opRequest = JsonConvert.DeserializeObject<OperationRequest>(httpContent);

            if (opRequest.Selectors?.Any() != true)
            {
                return new BadRequestObjectResult("At least one selector is required."); // 400
            }

            try
            {
                // Make the topic selectors GitHub friendly...

                opRequest.Selectors = opRequest.Selectors
                    .Select(s => new string(s.Trim().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).ToLower())
                    .ToList();

                log.LogInformation($"Attempting to dispatch operation [{opRequest.OperationId}]...");

                log.LogInformation(
                    $"Operation [{opRequest.OperationId}] selectors are " + 
                    $"{string.Join(" and ", opRequest.Selectors.Select(s => $"[{s}]"))}.");

                var pat = Environment.GetEnvironmentVariable("GitHubPat");
                var storageConnString = Environment.GetEnvironmentVariable("StorageConnectionString");
                var opContainerName = Environment.GetEnvironmentVariable("OperationStorageContainerName");
                var repoMapContainerName = Environment.GetEnvironmentVariable("RepoMapStorageContainerName");
                var repoOwner = Environment.GetEnvironmentVariable("RepoOwnerName");

                var blobServiceClient = new BlobServiceClient(storageConnString);
                var repoMapContainerClient = blobServiceClient.GetBlobContainerClient(repoMapContainerName);
                var repoMap = await repoMapContainerClient.GetRepoMapAsync();

                if (repoMap == null)
                {
                    log.LogWarning($"Repo map not found. Unable to service operation [{opRequest.OperationId}] request (503 Service Unavailable).");

                    return new StatusCodeResult(503); // 503 = Service Unavailable
                }

                log.LogInformation($"Selecting candidate workflows to service operation [{opRequest.OperationId}]...");

                var selectedRepos = SelectCandidateRepos(repoMap, opRequest);

                if (!selectedRepos.Any())
                {
                    log.LogWarning($"No workflows available to service operation [{opRequest.OperationId}] (404 Not Found).");

                    return WorkflowNotFound();
                }

                if (selectedRepos.Count > 1)
                {
                    log.LogWarning(
                        $"Can't decide which workflow to use to service operation [{opRequest.OperationId}] -- " +
                        $"{string.Join(" or ", selectedRepos.Select(r => $"[{r.Name}]"))}. This may indicate a " +
                        "workflow configuration problem (409 Conflict).");

                    return CantDecideWhichWorkflow(selectedRepos.Select(r => r.Name)); // 409
                }

                var selectedRepo = selectedRepos.Single();

                log.LogInformation($"Trying to dispatch operation [{opRequest.OperationId}] to selected repo/workflow [{selectedRepo.Name}/{actionType}].yml...");

                var ghHttpClient = CreateGitHubHttpClient(pat);
                var headSha = await ghHttpClient.GetHeadShaAsync(repoOwner, selectedRepo.Name);

                log.LogInformation($"Selected repo [{selectedRepo.Name}] main branch head SHA is [{headSha}].");

                if (!await ghHttpClient.DoesWorkflowExistAsync(repoOwner, selectedRepo.Name, actionType))
                {
                    log.LogWarning(
                        $"Unable to service operation [{opRequest.OperationId}]. " +
                        $"Selected repo [{selectedRepo.Name}] workflow [{actionType}.yml] not found (404 Not Found).");

                    return WorkflowNotFound(); // 404
                }

                log.LogInformation($"Creating operation [{opRequest.OperationId}] working branch...");

                var opBranchName = await ghHttpClient.CreateOperationBranchAsync(
                    repoOwner, selectedRepo.Name, headSha, opRequest.OperationId);

                log.LogInformation($"Created operational [{opRequest.OperationId}] working repo/branch [{selectedRepo.Name}/{opBranchName}].");

                var operation = new Operation
                {
                    ActionTypeName = actionType,
                    Context = opRequest.Context,
                    OperationId = opRequest.OperationId,
                    RepoName = selectedRepo.Name,
                    Selectors = opRequest.Selectors,
                    SubscriptionId = subscriptionId,
                    TenantId = tenantId
                };

                var opBlobContainerClient = blobServiceClient.GetBlobContainerClient(opContainerName);

                log.LogInformation($"Staging operation metadata [{opRequest.OperationId}] to working blob storage...");

                await opBlobContainerClient.PutOperationBlobAsync(operation);

                log.LogInformation(
                    $"Dispatching operation [{operation.OperationId}] to repo/branch/workflow " +
                    $"[{selectedRepo.Name}/{opBranchName}/{actionType}.yml]...");

                await ghHttpClient.DispatchWorkflowRunAsync(operation, repoOwner);
                await eventCollector.AddAsync(ToEventGridEvent(operation));

                return new AcceptedResult(); // 202
            }
            catch (Exception ex)
            {
                // Something broke. Log the error and return a 500...

                log.LogError(ex, $"An error occurred while attempting to dispatch operation [{opRequest.OperationId}]. See exception for details.");

                return new StatusCodeResult(500); // 500 = Internal Server Error
            }
        }

        private static IActionResult CantDecideWhichWorkflow(IEnumerable<string> repoNames) =>
            new ConflictObjectResult($"Can't decide which workflow to run -- {string.Join(" or ", repoNames.Select(rn => $"[{rn}]"))}");

        private static IActionResult WorkflowNotFound() =>
            new BadRequestObjectResult("No matching workflow found.");

        private static EventGridEvent ToEventGridEvent(Operation operation) =>
            new EventGridEvent
            {
                Data = new OperationEvent(operation),
                DataVersion = OperationEvent.DataVersion,
                EventTime = DateTime.UtcNow,
                EventType = EventTypeNames.SubscriptionConfiguring,
                Id = Guid.NewGuid().ToString(),
                Subject = $"/saas/tenants/{operation.TenantId}/subscriptions/{operation.SubscriptionId}"
            };

        private static HttpClient CreateGitHubHttpClient(string pat)
        {
            var httpClient = new HttpClient { BaseAddress = new Uri("https://api.github.com") };

            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.mercy-preview+json");
            httpClient.DefaultRequestHeaders.Add("Authorization", $"token {pat}");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "SaaS-Lifecycle");

            return httpClient;

        }
        private static async Task DispatchWorkflowRunAsync(this HttpClient httpClient, Operation operation, string repoOwner)
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post,
                $"/repos/{repoOwner}/{operation.RepoName}/actions/workflows/{operation.ActionTypeName}.yml/dispatches");

            var jsonContent = JsonConvert.SerializeObject(
                new { @ref = operation.OperationId, inputs = CreateWorkflowInputs(operation) });

            httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var httpResponse = await httpClient.SendAsync(httpRequest);

            if (!httpResponse.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"An error occurred while attempting to dispatch workflow operation [{operation.OperationId}]: " +
                    $"[{httpResponse.StatusCode.ToDescription()}]");
            }
        }

        private static Dictionary<string, string> CreateWorkflowInputs(Operation operation)
        {
            var inputs = new Dictionary<string, string>
            {
                ["operationId"] = operation.OperationId,
                ["subscriptionId"] = operation.SubscriptionId,
                ["tenantId"] = operation.TenantId
            };

            if (operation.Context != null)
            {
                inputs["context"] = operation.Context.ToString();
            }

            return inputs;
        }

        private static async Task<List<Repo>> GetRepoMapAsync(this BlobContainerClient containerClient)
        {
            const string mapBlobName = "repo_map.json";

            var repoMapBlobClient = containerClient.GetBlobClient(mapBlobName);

            if (!await repoMapBlobClient.ExistsAsync()) return null;

            var repoMapBlobContent = await repoMapBlobClient.DownloadContentAsync();
            var repoMapBlobString = Encoding.UTF8.GetString(repoMapBlobContent.Value.Content);

            return JsonConvert.DeserializeObject<List<Repo>>(repoMapBlobString);
        }

        private static async Task PutOperationBlobAsync(this BlobContainerClient containerClient, Operation operation)
        {
            var opBlobName = $"{operation.RepoName}/{operation.OperationId}";
            var opBlobClient = containerClient.GetBlobClient(opBlobName);
            var opBlobJson = JsonConvert.SerializeObject(operation);
            var opBlobBytes = Encoding.UTF8.GetBytes(opBlobJson);
            var opBlobData = new BinaryData(opBlobBytes);

            await opBlobClient.UploadAsync(opBlobData, overwrite: true);
        }

        private static async Task<string> CreateOperationBranchAsync(this HttpClient httpClient,
            string repoOwner, string repoName, string fromSha, string operationId)
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post,
                $"/repos/{repoOwner}/{repoName}/git/refs");

            var jsonContent = JsonConvert.SerializeObject(
                new { sha = fromSha, @ref = $"refs/heads/{operationId}" });

            httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var httpResponse = await httpClient.SendAsync(httpRequest);

            if (httpResponse.IsSuccessStatusCode)
            {
                return operationId;
            }
            else
            {
                throw new Exception(
                    $"Unexpected status code returned when trying to create operation [{operationId}] branch " +
                    $"[{repoOwner}/{repoName}/{operationId}]: [{httpResponse.StatusCode.ToDescription()}]");
            }
        }

        private static async Task<bool> DoesWorkflowExistAsync(this HttpClient httpClient,
            string repoOwner, string repoName, string actionTypeName)
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get,
                $"/repos/{repoOwner}/{repoName}/actions/workflows/{actionTypeName}.yml");

            var httpResponse = await httpClient.SendAsync(httpRequest);

            return httpResponse.StatusCode switch
            {
                HttpStatusCode.OK => true,
                HttpStatusCode.NotFound => false,
                _ => throw new Exception(
                    $"Unexpected status code returned when confirming that repo [{repoOwner}/{repoName}] " +
                    $"workflow [{actionTypeName}].yml exists: [{httpResponse.StatusCode.ToDescription()}].")
            };
        }

        private static async Task<string> GetHeadShaAsync(this HttpClient httpClient, string repoOwner, string repoName)
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/repos/{repoOwner}/{repoName}/commits?per_page=1");
            var httpResponse = await httpClient.SendAsync(httpRequest);

            httpResponse.EnsureSuccessStatusCode();

            var httpContent = await httpResponse.Content.ReadAsStringAsync();
            var commits = JsonConvert.DeserializeObject<IList<Commit>>(httpContent);
            var commit = commits.SingleOrDefault();

            if (commit == null)
            {
                throw new Exception($"Selected repo [{repoOwner}/{repoName}] has no commits. Unable to dispatch request.");
            }

            return commit.Sha;
        }

        private static string ToDescription(this HttpStatusCode statusCode) => $"{statusCode} ({(int)statusCode})";

        private static IList<Repo> SelectCandidateRepos(List<Repo> repoMap, OperationRequest opRequest) =>
            repoMap.Where(rm => opRequest.Selectors.All(s => rm.Topics.Contains(s))).ToList();
    }
}
