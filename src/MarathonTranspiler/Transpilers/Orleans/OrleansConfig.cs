using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.Orleans
{
    public class OrleansConfig
    {
        [JsonPropertyName("stateful")]
        public bool Stateful { get; set; }

        [JsonPropertyName("grainKeyTypes")]
        public Dictionary<string, string> GrainKeyTypes { get; set; }

        [JsonPropertyName("streams")]
        public Dictionary<string, List<string>> Streams { get; set; }

        [JsonPropertyName("testFramework")]
        public string TestFramework { get; set; } = "xunit";
    }
}
