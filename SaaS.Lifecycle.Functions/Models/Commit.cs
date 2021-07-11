using Newtonsoft.Json;

namespace SaaS.Lifecycle.Functions.Models
{
    public class Commit
    {
        [JsonProperty("sha")]
        public string Sha { get; set; }

        public override string ToString() => Sha;
    }
}
