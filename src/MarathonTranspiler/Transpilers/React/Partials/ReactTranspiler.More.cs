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
        // Dictionary to track method code blocks by ID for React components
        private readonly Dictionary<string, List<string>> _methodsById = new();

        protected override void ProcessMore(TranspiledClass currentClass, AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var id = annotation.Values.First(v => v.Key == "id").Value;

            // Process the code for control flow syntax
            var processedCode = ProcessControlFlowSyntax(block.Code);

            // Check if we have already encountered this method ID
            if (_methodsById.ContainsKey(id))
            {
                // If the method exists, extend it with additional code
                if (block.Annotations.Any(a => a.Name == "condition"))
                {
                    var conditionAnnotation = block.Annotations.First(x => x.Name == "condition");
                    var expression = conditionAnnotation.Values.First(v => v.Key == "expression").Value;

                    // Add conditional code
                    _methodsById[id].Add($"if ({expression}) {{");
                    _methodsById[id].AddRange(processedCode.Select(line => $"  {line}"));
                    _methodsById[id].Add("}");
                }
                else
                {
                    // Add code directly
                    _methodsById[id].AddRange(processedCode);
                }
            }
            else
            {
                // If this is the first occurrence of this method ID, store the code for later use
                // This handles the case where @more appears before the corresponding @run
                _methodsById[id] = new List<string>();

                if (block.Annotations.Any(a => a.Name == "condition"))
                {
                    var conditionAnnotation = block.Annotations.First(x => x.Name == "condition");
                    var expression = conditionAnnotation.Values.First(v => v.Key == "expression").Value;

                    _methodsById[id].Add($"if ({expression}) {{");
                    _methodsById[id].AddRange(processedCode.Select(line => $"  {line}"));
                    _methodsById[id].Add("}");
                }
                else
                {
                    _methodsById[id].AddRange(processedCode);
                }

                // Store the method to add it to the component later
                _mainMethodLines.Add($"// Additional code for method with ID '{id}' will be added when the method is defined");
            }
        }

        // Helper method to get stored code for a method ID
        public List<string> GetAdditionalCodeForMethodId(string id)
        {
            if (_methodsById.TryGetValue(id, out var code))
            {
                return code;
            }
            return new List<string>();
        }
    }
}
