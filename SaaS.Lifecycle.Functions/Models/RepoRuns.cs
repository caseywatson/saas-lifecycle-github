using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SaaS.Lifecycle.Functions.Models
{
    public class RepoRuns
    {
        [JsonPropertyName("workflow_runs")]
        public List<Run> Runs { get; set; }
    }
}
