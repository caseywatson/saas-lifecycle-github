using Newtonsoft.Json;
using System;

namespace Edgar.Functions.Models
{
    public class Run
    {
        public static class Conclusions
        {
            public const string ActionRequired = "action_required";
            public const string Cancelled = "cancelled";
            public const string Failure = "failure";
            public const string Neutral = "neutral";
            public const string Success = "success";
            public const string Skipped = "skipped";
            public const string Stale = "stale";
            public const string TimedOut = "timed_out";
        }

        [JsonProperty("id")]
        public string RunId { get; set; }

        [JsonProperty("head_branch")]
        public string BranchName { get; set; }

        [JsonProperty("conclusion")]
        public string Conclusion { get; set; }

        [JsonProperty("html_url")]
        public string RunHtmlUrl { get; set; }

        [JsonProperty("created_at")]
        public DateTime? CreatedAtUtc { get; set; }

        [JsonProperty("updated_at")]
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
