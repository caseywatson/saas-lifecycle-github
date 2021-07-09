using System.Collections.Generic;
using System.Linq;

namespace SaaS.Lifecycle.Functions.Models
{
    public class CandidateRepo
    {
        public const string LabelPrefix = "saas/";
        public const string OptInLabel = "saas/lifecycle";

        public CandidateRepo() { }

        public CandidateRepo(Repo repo)
        {
            Repo = repo;
            Labels = ParseRepoTopics(repo);
        }

        public Repo Repo { get; set; }

        public Dictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();

        private Dictionary<string, string> ParseRepoTopics(Repo repo) => repo.Topics
            // Does the topic have a label prefix?
            .Where(t => t.StartsWith(LabelPrefix))
            // If so, turn "saas/label : value" into [ "label", "value" ]...
            .Select(t => t.Substring(LabelPrefix.Length).Split(':', 2).Select(p => p.Trim()).ToArray())
            // Make sure that we have a label and a value.
            .Where(a => (a.Length == 2))
            // Dump the output into a dictionary...
            .ToDictionary(a => a[0], a => a[1]);
    }
}

