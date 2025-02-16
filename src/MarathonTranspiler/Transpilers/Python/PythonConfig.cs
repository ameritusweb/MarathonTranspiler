using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.Python
{
    public class PythonConfig
    {
        [JsonPropertyName("scientificImports")]
        public List<string> ScientificImports { get; set; } = new();  // numpy, sympy etc.

        [JsonPropertyName("useTypeHints")]
        public bool UseTypeHints { get; set; }
    }
}
