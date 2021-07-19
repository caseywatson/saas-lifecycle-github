using Newtonsoft.Json;

namespace Edgar.Functions.Models
{
    public class Commit
    {
        [JsonProperty("sha")]
        public string Sha { get; set; }

        public override string ToString() => Sha;
    }
}
