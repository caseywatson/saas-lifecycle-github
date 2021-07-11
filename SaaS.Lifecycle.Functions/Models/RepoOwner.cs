using Newtonsoft.Json;

namespace SaaS.Lifecycle.Functions.Models
{
    public class RepoOwner
    {
        [JsonProperty("login")]
        public string Name { get; set; }

        public override string ToString() => Name;
    }
}
