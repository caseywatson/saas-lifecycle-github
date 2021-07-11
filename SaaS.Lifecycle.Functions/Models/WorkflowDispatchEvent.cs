using Newtonsoft.Json;
using System.Collections.Generic;

namespace SaaS.Lifecycle.Functions.Models
{
    public class WorkflowDispatchEvent
    {
        [JsonProperty("ref")]
        public string Ref { get; set; }

        [JsonProperty("inputs")]
        public Dictionary<string, string> Inputs { get; set; } = new Dictionary<string, string>();
    } 
}
