using MarathonTranspiler.Transpilers.CSharp;
using MarathonTranspiler.Transpilers.FullStackWeb;
using MarathonTranspiler.Transpilers.Orleans;
using MarathonTranspiler.Transpilers.Python;
using MarathonTranspiler.Transpilers.React;
using MarathonTranspiler.Transpilers.ReactRedux;
using MarathonTranspiler.Transpilers.Unity;
using MarathonTranspiler.Transpilers.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MarathonTranspiler
{
    public class TranspilerOptions
    {
        [JsonPropertyName("target")]
        public string Target { get; set; }

        [JsonPropertyName("csharp")]
        public CSharpConfig CSharp { get; set; }

        [JsonPropertyName("orleans")]
        public OrleansConfig Orleans { get; set; }

        [JsonPropertyName("unity")]
        public UnityConfig Unity { get; set; }

        [JsonPropertyName("python")]
        public PythonConfig Python { get; set; }

        [JsonPropertyName("react")]
        public ReactConfig React { get; set; }

        [JsonPropertyName("react-redux")]
        public ReactReduxConfig ReactRedux { get; set; }

        [JsonPropertyName("fullstackweb")]
        public FullStackWebConfig FullStackWeb { get; set; }

        [JsonPropertyName("epf")]
        public WpfConfig Wpf { get; set; }
    }
}
