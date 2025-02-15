using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MarathonTranspiler
{
    public class Config
    {
        [JsonPropertyName("transpilerOptions")]
        public TranspilerOptions TranspilerOptions { get; set; }

        [JsonPropertyName("include")]
        public List<string> Include { get; set; }

        [JsonPropertyName("exclude")]
        public List<string> Exclude { get; set; }

        [JsonPropertyName("rootDir")]
        public string RootDirectory { get; set; }
    }
}
