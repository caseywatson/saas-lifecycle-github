﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Edgar.Functions.Models
{
    public class OperationRequest
    {
        [JsonProperty("id")]
        public string OperationId { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("context")]
        public JObject Context { get; set; }

        [JsonProperty("selectors")]
        public List<string> Selectors { get; set; } = new List<string>();
    }
}
