using System;
using System.Text.Json.Serialization;

namespace SaaS.Lifecycle.Functions.Models
{
    public class Run
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("conclusion")]
        public string Conclusion { get; set; }

        [JsonPropertyName("html_url")]
        public string RunHtmlUrl { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAtUtc { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
