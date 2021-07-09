using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SaaS.Lifecycle.Functions.Models
{
    public class Operation
    {
        [JsonPropertyName("operationId")]
        public string OperationId { get; set; }

        [JsonPropertyName("runId")]
        public string RunId { get; set; }

        [JsonPropertyName("workflowId")]
        public string WorkflowId { get; set; }

        [JsonPropertyName("subscriptionId")]
        public string SubscriptionId { get; set; }

        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; }

        [JsonPropertyName("context")]
        public JsonElement? Context { get; set; }

        [JsonPropertyName("selectors")]
        public Dictionary<string, string> Selectors { get; set; } = new Dictionary<string, string>();
    }
}
