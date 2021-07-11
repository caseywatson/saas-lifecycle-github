﻿using Newtonsoft.Json;
using System.Linq;

namespace SaaS.Lifecycle.Functions.Models
{
    public class Repo
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("full_name")]
        public string FullName { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("default_branch")]
        public string DefaultBranchName { get; set; }

        [JsonProperty("html_url")]
        public string RepoHomeUrl { get; set; }

        [JsonProperty("topics")]
        public string[] Topics { get; set; }

        [JsonProperty("owner")]
        public RepoOwner Owner { get; set; }

        public bool IsPotentialCandidate() => (Topics?.Contains(CandidateRepo.OptInLabel) == true);
    }
}
