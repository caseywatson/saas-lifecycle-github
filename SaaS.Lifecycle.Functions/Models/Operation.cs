using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text.Json;

namespace SaaS.Lifecycle.Functions.Models
{
    public class Operation
    {
        [JsonProperty("operationId")]
        public string OperationId { get; set; }

        [JsonProperty("subscriptionId")]
        public string SubscriptionId { get; set; }

        [JsonProperty("tenantId")]
        public string TenantId { get; set; }

        [JsonProperty("runId")]
        public string RunId { get; set; }

        [JsonProperty("workflowId")]
        public string WorkflowId { get; set; }

        [JsonProperty("actionType")]
        public string ActionTypeName { get; set; }

        [JsonProperty("context")]
        public JObject Context { get; set; }

        [JsonProperty("selectors")]
        public Dictionary<string, string> Selectors { get; set; } = new Dictionary<string, string>();
    }
}
