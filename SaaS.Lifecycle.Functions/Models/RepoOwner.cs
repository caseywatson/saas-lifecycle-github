using System.Text.Json.Serialization;

namespace SaaS.Lifecycle.Functions.Models
{
    public class RepoOwner
    {
        [JsonPropertyName("login")]
        public string Name { get; set; }

        public override string ToString() => Name;
    }
}
