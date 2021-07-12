using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace SaaS.Lifecycle.Functions.Models
{
    public class OperationEvent
    {
        public const string DataVersion = "0.1";

        public OperationEvent() { }

        public OperationEvent(Operation operation)
        {
            OperationId = operation.OperationId;
            SubscriptionId = operation.SubscriptionId;
            TenantId = operation.TenantId;
            ActionTypeName = operation.ActionTypeName;
            Context = operation.Context;
            Selectors = operation.Selectors;
            RepoName = operation.RepoName;
        }

        public OperationEvent(Operation operation, Run run) : this(operation)
        {
            RunId = run.RunId;
            Conclusion = run.Conclusion;
            RunUrl = run.RunHtmlUrl;
            StartedAtUtc = run.CreatedAtUtc;
            FinishedAtUtc = run.UpdatedAtUtc;
        }

        [JsonProperty("operationId")]
        public string OperationId { get; set; }

        [JsonProperty("subscriptionId")]
        public string SubscriptionId { get; set; }

        [JsonProperty("tenantId")]
        public string TenantId { get; set; }

        [JsonProperty("actionType")]
        public string ActionTypeName { get; set; }

        [JsonProperty("repoName")]
        public string RepoName { get; set; }

        [JsonProperty("context")]
        public JObject Context { get; set; }

        [JsonProperty("selectors")]
        public List<string> Selectors { get; set; } = new List<string>();

        [JsonProperty("runId", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string RunId { get; set; }

        [JsonProperty("conclusion", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Conclusion { get; set; }

        [JsonProperty("runUrl", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string RunUrl { get; set; }

        [JsonProperty("startedAtUtc", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public DateTime? StartedAtUtc { get; set; }

        [JsonProperty("finishedAtUtc", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public DateTime? FinishedAtUtc { get; set; }

        public string ToSubject() => $"/saas/tenants/{TenantId}/subscriptions/{SubscriptionId}";
    }
}
