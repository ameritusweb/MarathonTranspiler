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
    }
}
