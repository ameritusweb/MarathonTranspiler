using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.React
{
    public class ReactConfig
    {
        [JsonPropertyName("typescript")]
        public bool UseTypeScript { get; set; }

        [JsonPropertyName("hooks")]
        public List<string> IncludedHooks { get; set; } = new();  // useState, useEffect etc.

        [JsonPropertyName("name")]
        public string Name { get; set; } = "App";

        [JsonPropertyName("testFramework")]
        public string TestFramework { get; set; } = "jest"; // jest, testing-library, etc.

        [JsonPropertyName("flowSystem")]
        public bool UseFlowSystem { get; set; } = true;

        [JsonPropertyName("strictMode")]
        public bool UseStrictMode { get; set; } = false;

        [JsonPropertyName("errorBoundary")]
        public bool GenerateErrorBoundary { get; set; } = false;
    }
}
