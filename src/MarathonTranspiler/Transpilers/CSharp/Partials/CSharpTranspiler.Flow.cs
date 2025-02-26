using MarathonTranspiler.Core;
using MarathonTranspiler.Extensions;
using MarathonTranspiler.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.CSharp
{
    public partial class CSharpTranspiler : MarathonTranspilerBase
    {
        private readonly Dictionary<string, List<string>> _flows = new();
        private readonly Dictionary<string, Dictionary<string, string>> _flowVars = new();

        protected override void ProcessFlow(TranspiledClass currentClass, AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var flowName = annotation.Values.First(v => v.Key == "name").Value;

            // Store the flow's code for later use
            _flows[flowName] = block.Code;

            // Create a dictionary to store flow-specific variables
            if (!_flowVars.ContainsKey(flowName))
            {
                _flowVars[flowName] = new Dictionary<string, string>();
            }
        }
    }
}
