using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.Unity
{
    public class UnityConfig
    {
        [JsonPropertyName("outputPath")]
        public string OutputPath { get; set; }

        [JsonPropertyName("generatePrefabs")]
        public bool GeneratePrefabs { get; set; }
    }
}
