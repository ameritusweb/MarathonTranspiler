using MarathonTranspiler.Core;
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
                var directFlowMatch = Regex.Match(trimmedLine, @"^\{([^}]+)\}$");

                if (directFlowMatch.Success)
                {
                    var flowName = directFlowMatch.Groups[1].Value;
                    processedCode.AddRange(ProcessDirectFlowReference(flowName));
                    continue;
                }

                // Check for control flow syntax with loop and expression: ``@loop [expr] {flowName}
                var loopMatch = Regex.Match(trimmedLine, @"``@loop\s+\[(.*?)\]\s+\{([^}]+)\}");
                if (loopMatch.Success)
                {
                    var flowName = loopMatch.Groups[2].Value;
                    processedCode.AddRange(ProcessLoopSyntax(line, flowName, methodName, currentClass));
                    continue;
                }

                // Check for control flow syntax with expression: ``@if (expr) {flowName}
                var controlFlowWithExprMatch = Regex.Match(trimmedLine, @"``@(\w+)\s+(\([^)]+\))\s+\{([^}]+)\}");
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

                // Check for simple control flow syntax: ``@keyword {flowName}
                var controlFlowMatch = Regex.Match(trimmedLine, @"``@(\w+)\s+\{([^}]+)\}");
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
            var loopExprMatch = Regex.Match(line.Trim(), @"``@loop\s+\[(.*?)\]");

            if (!loopExprMatch.Success || !_flows.ContainsKey(flowName))
            {
                // If no valid expression or flow name, return the original line
                return new List<string> { line };
            }

            var loopExpr = loopExprMatch.Groups[1].Value;
            var parts = loopExpr.Split(':');

            if (parts.Length == 1)
            {
                // Simple loop over a collection: [collection]
                result.Add($"foreach (var item in {parts[0].Trim()}) {{");

                // Process the flow code to handle any nested flow references
                var flowCode = new List<string>(_flows[flowName]);
                var processedFlowCode = ProcessControlFlowSyntax(flowCode, methodName, currentClass);
                result.AddRange(processedFlowCode.Select(l => $"    {l}"));

                result.Add("}");
            }
            else if (parts.Length == 2)
            {
                // Either [item:collection] or [i=start:end]
                var firstPart = parts[0].Trim();
                var secondPart = parts[1].Trim();

                if (firstPart.Contains("="))
                {
                    // Numeric range: [i=1:10]
                    var rangeMatch = Regex.Match(loopExpr, @"(\w+)=(\d+):(\d+)");
                    if (rangeMatch.Success)
                    {
                        var varName = rangeMatch.Groups[1].Value;
                        var start = rangeMatch.Groups[2].Value;
                        var end = rangeMatch.Groups[3].Value;

                        // Determine if inclusive (square brackets) or exclusive (parentheses)
                        bool isInclusive = line.Contains("[");

                        result.Add($"for (int {varName} = {start}; {varName} {(isInclusive ? "<=" : "<")} {end}; {varName}++) {{");

                        // Process the flow code to handle any nested flow references
                        var flowCode = new List<string>(_flows[flowName]);
                        var processedFlowCode = ProcessControlFlowSyntax(flowCode, methodName, currentClass);
                        result.AddRange(processedFlowCode.Select(l => $"    {l}"));

                        result.Add("}");
                    }
                }
                else
                {
                    // Basic iteration with item variable: [item:collection]
                    result.Add($"foreach (var {firstPart} in {secondPart}) {{");

                    // Process the flow code to handle any nested flow references
                    var flowCode = new List<string>(_flows[flowName]);
                    var processedFlowCode = ProcessControlFlowSyntax(flowCode, methodName, currentClass);
                    result.AddRange(processedFlowCode.Select(l => $"    {l}"));

                    result.Add("}");
                }
            }
            else if (parts.Length == 3)
            {
                // Filter or transform: [item:transform/condition:collection]
                var itemVar = parts[0].Trim();
                var operation = parts[1].Trim();
                var collection = parts[2].Trim();

                // Check if this is a condition (filtering) by looking for comparison operators
                bool isFilter = operation.Contains(">") || operation.Contains("<") ||
                               operation.Contains("==") || operation.Contains("!=") ||
                               operation.StartsWith("!") || operation.Contains("&&") ||
                               operation.Contains("||");

                bool isTransform = !isFilter;

                if (isTransform)
                {
                    // Transformation (Select): [t:t.ToUpper():myStrings]
                    // operation should reference the item variable
                    string transformLambda = operation.Replace(itemVar, "x");
                    result.Add($"foreach (var {itemVar} in {collection}.Select(x => {transformLambda})) {{");

                    // Process the flow code to handle any nested flow references
                    var flowCode = new List<string>(_flows[flowName]);
                    var processedFlowCode = ProcessControlFlowSyntax(flowCode, methodName, currentClass);
                    result.AddRange(processedFlowCode.Select(l => $"    {l}"));

                    result.Add("}");
                }
                else if (isFilter)
                {
                    // Filtering (Where): [x:x > 5:numbers]
                    string filterLambda = operation.Replace(itemVar, "x");
                    result.Add($"foreach (var {itemVar} in {collection}.Where(x => {filterLambda})) {{");

                    // Process the flow code to handle any nested flow references
                    var flowCode = new List<string>(_flows[flowName]);
                    var processedFlowCode = ProcessControlFlowSyntax(flowCode, methodName, currentClass);
                    result.AddRange(processedFlowCode.Select(l => $"    {l}"));

                    result.Add("}");
                }
            }
            else if (parts.Length > 3)
            {
                // Chained operations: [result:transform:filter:collection]
                var itemVar = parts[0].Trim();
                var operations = parts.Skip(1).Take(parts.Length - 2).ToList();
                var collection = parts[parts.Length - 1].Trim();

                // Start with the collection
                var queryExpr = collection;

                // Apply each operation in sequence
                foreach (var op in operations)
                {
                    if (op.Contains(">") || op.Contains("<") || op.Contains("==") ||
                        op.Contains("!=") || op.StartsWith("!") || op.Contains("&&") ||
                        op.Contains("||"))
                    {
                        // This is a filter
                        string filterLambda = op.Replace(itemVar, "x");
                        queryExpr = $"{queryExpr}.Where(x => {filterLambda})";
                    }
                    else
                    {
                        // This is a transform
                        string transformLambda = op.Replace(itemVar, "x");
                        queryExpr = $"{queryExpr}.Select(x => {transformLambda})";
                    }
                }

                result.Add($"foreach (var {itemVar} in {queryExpr}) {{");

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

            // Extract condition from if statement
            var conditionMatch = Regex.Match(line.Trim(), @"``@if\s+(\([^)]+\))\s+\{");

            if (conditionMatch.Success && _flows.ContainsKey(flowName))
            {
                var condition = conditionMatch.Groups[1].Value;
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
            var switchMatch = Regex.Match(line.Trim(), @"``@switch\s+(\([^)]+\))\s+\{");

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
            var caseMatch = Regex.Match(line.Trim(), @"``@case\s+(\S+)\s+\{");

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
