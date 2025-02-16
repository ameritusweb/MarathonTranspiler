using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.CSharp
{
    public class CSharpConfig
    {
        [JsonPropertyName("testFramework")]
        public string TestFramework { get; set; } = "xunit"; // or "nunit"
    }
}
