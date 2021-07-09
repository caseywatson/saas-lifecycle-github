using System.Text.Json.Serialization;

namespace SaaS.Lifecycle.Functions.Models
{
    public class Repo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("full_name")]
        public string FullName { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("default_branch")]
        public string DefaultBranchName { get; set; }

        [JsonPropertyName("html_url")]
        public string RepoHomeUrl { get; set; }

        [JsonPropertyName("topics")]
        public string[] Topics { get; set; }

        [JsonPropertyName("owner")]
        public RepoOwner Owner { get; set; }
    }
}
