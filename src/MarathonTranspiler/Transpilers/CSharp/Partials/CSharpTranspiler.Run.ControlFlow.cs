using MarathonTranspiler.Core;
using MarathonTranspiler.Helpers;
using MarathonTranspiler.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.CSharp
{
    public partial class CSharpTranspiler : MarathonTranspilerBase
    {
        private List<string> ProcessControlFlowSyntax(List<string> code, string methodName, TranspiledClass currentClass)
        {
            List<string> processedCode = new List<string>();

            for (int i = 0; i < code.Count; i++)
            {
                var line = code[i];
                var trimmedLine = line.Trim();

                // Check for direct flow reference: {flowName}
                if (FlowProcessingHelper.IsDirectFlowReference(trimmedLine))
                {
                    var flowName = FlowProcessingHelper.ExtractDirectFlowName(trimmedLine);
                    processedCode.AddRange(ProcessDirectFlowReference(flowName));
                    continue;
                }

                // Check for control flow syntax with loop and expression: --@loop [expr] {flowName}
                var loopMatch = Regex.Match(trimmedLine, @"--@loop\s+\[(.*?)\]\s+\{([^}]+)\}");
                if (loopMatch.Success)
                {
                    var flowName = loopMatch.Groups[2].Value;
                    processedCode.AddRange(ProcessLoopSyntax(line, flowName, methodName, currentClass));
                    continue;
                }

                // Check for control flow syntax with expression: --@if (expr) {flowName}
                var controlFlowWithExprMatch = Regex.Match(trimmedLine, @"--@(\w+)\s+(\([^)]+\))\s+\{([^}]+)\}");
                if (controlFlowWithExprMatch.Success)
                {
                    var keyword = controlFlowWithExprMatch.Groups[1].Value;
                    var expression = controlFlowWithExprMatch.Groups[2].Value;
                    var flowName = controlFlowWithExprMatch.Groups[3].Value;

                    switch (keyword.ToLower())
                    {
                        case "if":
                            processedCode.AddRange(ProcessIfWithExprSyntax(expression, flowName, methodName, currentClass));
                            break;
                        case "switch":
                            processedCode.AddRange(ProcessSwitchWithExprSyntax(expression, flowName, methodName, currentClass));
                            break;
                        default:
                            // Keep the original line if keyword is not recognized
                            processedCode.Add(line);
                            break;
                    }
                    continue;
                }

                // Check for simple control flow syntax: --@keyword {flowName}
                var controlFlowMatch = Regex.Match(trimmedLine, @"--@(\w+)\s+\{([^}]+)\}");
                if (controlFlowMatch.Success)
                {
                    var keyword = controlFlowMatch.Groups[1].Value;
                    var flowName = controlFlowMatch.Groups[2].Value;

                    switch (keyword.ToLower())
                    {
                        case "if":
                            processedCode.AddRange(ProcessIfSyntax(line, flowName, methodName, currentClass));
                            break;

                        case "else":
                            processedCode.AddRange(ProcessElseSyntax(line, flowName, methodName, currentClass));
                            break;

                        case "case":
                            processedCode.AddRange(ProcessCaseSyntax(line, flowName, methodName, currentClass));
                            break;

                        default:
                            // Keep the original line if keyword is not recognized
                            processedCode.Add(line);
                            break;
                    }
                    continue;
                }

                // Keep lines without control flow syntax unchanged
                processedCode.Add(line);
            }

            return processedCode;
        }

        private List<string> ProcessDirectFlowReference(string flowName)
        {
            if (!_flows.ContainsKey(flowName))
            {
                // If flow is not found, return a comment
                return new List<string> { $"// Flow '{flowName}' not found" };
            }

            // Simply insert the flow's code directly
            return new List<string>(_flows[flowName]);
        }

        private List<string> ProcessLoopSyntax(string line, string flowName, string methodName, TranspiledClass currentClass)
        {
            var result = new List<string>();

            // Extract the loop expression from the line
            var loopExprMatch = Regex.Match(line.Trim(), @"--@loop\s+\[(.*?)\]");

            if (!loopExprMatch.Success || !_flows.ContainsKey(flowName))
            {
                // If no valid expression or flow name, return the original line
                return new List<string> { line };
            }

            var loopExpr = loopExprMatch.Groups[1].Value;

            // Use the shared helper to parse the loop expression
            var (item, collection, operation) = FlowProcessingHelper.ParseLoopExpression(loopExpr);

            if (string.IsNullOrEmpty(operation))
            {
                if (item == "item" && !collection.Contains(":"))
                {
                    // Simple loop over a collection: [collection]
                    result.Add($"foreach (var item in {collection}) {{");

                    // Process the flow code to handle any nested flow references
                    var flowCode = new List<string>(_flows[flowName]);
                    var processedFlowCode = ProcessControlFlowSyntax(flowCode, methodName, currentClass);
                    result.AddRange(processedFlowCode.Select(l => $"    {l}"));

                    result.Add("}");
                }
                else
                {
                    // Basic iteration with item variable: [item:collection]
                    result.Add($"foreach (var {item} in {collection}) {{");

                    // Process the flow code to handle any nested flow references
                    var flowCode = new List<string>(_flows[flowName]);
                    var processedFlowCode = ProcessControlFlowSyntax(flowCode, methodName, currentClass);
                    result.AddRange(processedFlowCode.Select(l => $"    {l}"));

                    result.Add("}");
                }
            }
            else if (operation == "range")
            {
                // Numeric range: [i=1:10]
                var rangeParts = collection.Split(':');
                if (rangeParts.Length == 2)
                {
                    var start = rangeParts[0];
                    var end = rangeParts[1];

                    // Determine if inclusive (square brackets) or exclusive (parentheses)
                    bool isInclusive = line.Contains("[");

                    result.Add($"for (int {item} = {start}; {item} {(isInclusive ? "<=" : "<")} {end}; {item}++) {{");

                    // Process the flow code to handle any nested flow references
                    var flowCode = new List<string>(_flows[flowName]);
                    var processedFlowCode = ProcessControlFlowSyntax(flowCode, methodName, currentClass);
                    result.AddRange(processedFlowCode.Select(l => $"    {l}"));

                    result.Add("}");
                }
            }
            else if (operation.StartsWith("filter:"))
            {
                // Filtering (Where): [x:x > 5:numbers]
                string filterLambda = operation.Substring(7).Replace(item, "x");
                result.Add($"foreach (var {item} in {collection}.Where(x => {filterLambda})) {{");

                // Process the flow code to handle any nested flow references
                var flowCode = new List<string>(_flows[flowName]);
                var processedFlowCode = ProcessControlFlowSyntax(flowCode, methodName, currentClass);
                result.AddRange(processedFlowCode.Select(l => $"    {l}"));

                result.Add("}");
            }
            else if (operation.StartsWith("transform:"))
            {
                // Transformation (Select): [t:t.ToUpper():myStrings]
                string transformLambda = operation.Substring(10).Replace(item, "x");
                result.Add($"foreach (var {item} in {collection}.Select(x => {transformLambda})) {{");

                // Process the flow code to handle any nested flow references
                var flowCode = new List<string>(_flows[flowName]);
                var processedFlowCode = ProcessControlFlowSyntax(flowCode, methodName, currentClass);
                result.AddRange(processedFlowCode.Select(l => $"    {l}"));

                result.Add("}");
            }

            return result;
        }

        private List<string> ProcessIfSyntax(string line, string flowName, string methodName, TranspiledClass currentClass)
        {
            var result = new List<string>();

            // Extract condition from if statement using the helper
            var condition = FlowProcessingHelper.ExtractCondition(line);

            if (!string.IsNullOrEmpty(condition) && _flows.ContainsKey(flowName))
            {
                result.Add($"if {condition} {{");
                result.AddRange(_flows[flowName].Select(l => $"    {l}"));
                result.Add("}");
            }
            else
            {
                // Handle case where no explicit condition is provided
                // The flow name itself can be treated as a boolean variable or method
                result.Add($"if ({flowName}) {{");
                result.AddRange(_flows[flowName].Select(l => $"    {l}"));
                result.Add("}");
            }

            return result;
        }

        private List<string> ProcessElseSyntax(string line, string flowName, string methodName, TranspiledClass currentClass)
        {
            var result = new List<string>();

            if (_flows.ContainsKey(flowName))
            {
                result.Add("else {");
                result.AddRange(_flows[flowName].Select(l => $"    {l}"));
                result.Add("}");
            }

            return result;
        }

        private List<string> ProcessSwitchSyntax(string line, string flowName, string methodName, TranspiledClass currentClass)
        {
            var result = new List<string>();

            // Extract the switch expression
            var switchMatch = Regex.Match(line.Trim(), @"--@switch\s+(\([^)]+\))\s+\{");

            if (switchMatch.Success && _flows.ContainsKey(flowName))
            {
                var switchExpr = switchMatch.Groups[1].Value;
                result.Add($"switch {switchExpr} {{");
                result.AddRange(_flows[flowName].Select(l => $"    {l}"));
                result.Add("}");
            }

            return result;
        }

        private List<string> ProcessCaseSyntax(string line, string flowName, string methodName, TranspiledClass currentClass)
        {
            var result = new List<string>();

            // Extract the case value
            var caseMatch = Regex.Match(line.Trim(), @"--@case\s+(\S+)\s+\{");

            if (caseMatch.Success && _flows.ContainsKey(flowName))
            {
                var caseValue = caseMatch.Groups[1].Value;
                result.Add($"case {caseValue}:");
                result.AddRange(_flows[flowName].Select(l => $"    {l}"));
                result.Add("    break;");
            }

            return result;
        }

        private List<string> ProcessIfWithExprSyntax(string expression, string flowName, string methodName, TranspiledClass currentClass)
        {
            var result = new List<string>();

            if (_flows.ContainsKey(flowName))
            {
                result.Add($"if {expression} {{");

                // Process the flow code to handle any nested flow references
                var flowCode = new List<string>(_flows[flowName]);
                var processedFlowCode = ProcessControlFlowSyntax(flowCode, methodName, currentClass);
                result.AddRange(processedFlowCode.Select(l => $"    {l}"));

                result.Add("}");
            }
            else
            {
                // If flow is not found, add a comment and the original if statement
                result.Add($"// Flow '{flowName}' not found");
                result.Add($"if {expression} {{");
                result.Add("}");
            }

            return result;
        }

        private List<string> ProcessSwitchWithExprSyntax(string expression, string flowName, string methodName, TranspiledClass currentClass)
        {
            var result = new List<string>();

            if (_flows.ContainsKey(flowName))
            {
                result.Add($"switch {expression} {{");

                // Process the flow code to handle any nested flow references
                var flowCode = new List<string>(_flows[flowName]);
                var processedFlowCode = ProcessControlFlowSyntax(flowCode, methodName, currentClass);
                result.AddRange(processedFlowCode.Select(l => $"    {l}"));

                result.Add("}");
            }
            else
            {
                // If flow is not found, add a comment and the original switch statement
                result.Add($"// Flow '{flowName}' not found");
                result.Add($"switch {expression} {{");
                result.Add("}");
            }

            return result;
        }
    }
}