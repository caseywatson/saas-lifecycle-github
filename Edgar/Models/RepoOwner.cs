using Newtonsoft.Json;

namespace Edgar.Functions.Models
{
    public class RepoOwner
    {
        [JsonProperty("login")]
        public string Name { get; set; }

        public override string ToString() => Name;
    }
}
