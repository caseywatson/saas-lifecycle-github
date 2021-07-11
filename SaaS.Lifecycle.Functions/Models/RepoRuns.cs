using Newtonsoft.Json;
using System.Collections.Generic;

namespace SaaS.Lifecycle.Functions.Models
{
    public class RepoRuns
    {
        [JsonProperty("workflow_runs")]
        public List<Run> Runs { get; set; }
    }
}
