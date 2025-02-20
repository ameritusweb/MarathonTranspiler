using MarathonTranspiler.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.React
{
    public partial class ReactTranspiler : MarathonTranspilerBase
    {
        protected override void ProcessEvent(TranspiledClass currentClass, AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var eventName = annotation.Values.First(v => v.Key == "event").Value;
            var target = annotation.Values.First(v => v.Key == "target").Value;

            var handlerName = $"handle{target}{eventName}";
            _mainMethodLines.Add($"const {handlerName} = () => {{");
            _mainMethodLines.AddRange(block.Code.Select(line => $"    {line}"));
            _mainMethodLines.Add("};");

            _jsxOutput.AppendLine($"<button onClick={{{handlerName}}}>{target}</button>");
        }
    }
}
