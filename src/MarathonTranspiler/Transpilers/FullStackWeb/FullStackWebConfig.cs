using MarathonTranspiler.Transpilers.React;
using MarathonTranspiler.Transpilers.ReactRedux;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.FullStackWeb
{
    public class FullStackWebConfig
    {
        [JsonPropertyName("backend")]
        public AspNetConfig Backend { get; set; } = new();

        [JsonPropertyName("redux")]
        public ReactReduxConfig Redux { get; set; } = new();

        [JsonPropertyName("react")]
        public ReactConfig React { get; set; } = new();
    }
}
