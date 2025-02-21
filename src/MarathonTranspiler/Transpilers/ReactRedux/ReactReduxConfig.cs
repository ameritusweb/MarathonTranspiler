using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.ReactRedux
{
    public class ReactReduxConfig
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "Marathon App";

        // Whether to enable Redux DevTools
        [JsonPropertyName("devTools")]
        public bool DevTools { get; set; } = true;

        // Additional middleware to include
        [JsonPropertyName("middleware")]
        public List<string> Middleware { get; set; } = new() { "logger", "thunk" };
    }
}
