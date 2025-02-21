using MarathonTranspiler.Core;
using MarathonTranspiler.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.React
{
    public partial class ReactTranspiler : MarathonTranspilerBase
    {
        protected override void ProcessRun(TranspiledClass currentClass, AnnotatedCode block)
        {
            var functionName = block.Annotations[0].Values.First(v => v.Key == "functionName").Value;

            _mainMethodLines.Add($"const {functionName} = () => {{");
            _mainMethodLines.AddRange(block.Code.Select(line => $"    {line}"));
            _mainMethodLines.Add("};");
        }
    }
}
