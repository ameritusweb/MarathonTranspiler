using MarathonTranspiler.Core;
using MarathonTranspiler.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.React
{
    public partial class ReactTranspiler : MarathonTranspilerBase
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

        // Process flow references in React code
        private List<string> ProcessControlFlowSyntax(List<string> code)
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
                    processedCode.AddRange(ProcessLoopSyntax(line, flowName));
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
                            processedCode.AddRange(ProcessIfWithExprSyntax(expression, flowName));
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
                            processedCode.AddRange(ProcessIfSyntax(line, flowName));
                            break;

                        case "else":
                            processedCode.AddRange(ProcessElseSyntax(line, flowName));
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

            // Process the flow's code to handle any nested flow references
            var flowCode = new List<string>(_flows[flowName]);
            return ProcessControlFlowSyntax(flowCode);
        }

        private List<string> ProcessLoopSyntax(string line, string flowName)
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
                result.Add($"{{{parts[0].Trim()}.map(item => {{");
                result.AddRange(ProcessControlFlowSyntax(_flows[flowName]));
                result.Add("})}}");
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

                        // In React, we'll use Array.from to create a range
                        result.Add($"{{Array.from({{length: {end} - {start} + 1}}, (_, i) => i + {start}).map({varName} => {{");
                        result.AddRange(ProcessControlFlowSyntax(_flows[flowName]));
                        result.Add("})}}");
                    }
                }
                else
                {
                    // Basic iteration with item variable: [item:collection]
                    result.Add($"{{{secondPart}.map({firstPart} => {{");
                    result.AddRange(ProcessControlFlowSyntax(_flows[flowName]));
                    result.Add("})}}");
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
                    // Transformation (map): [t:t.toUpperCase():myStrings]
                    string transformLambda = operation.Replace(itemVar, "x");
                    result.Add($"{{{collection}.map(x => {transformLambda}).map({itemVar} => {{");
                    result.AddRange(ProcessControlFlowSyntax(_flows[flowName]));
                    result.Add("})}}");
                }
                else if (isFilter)
                {
                    // Filtering (filter): [x:x > 5:numbers]
                    string filterLambda = operation.Replace(itemVar, "x");
                    result.Add($"{{{collection}.filter(x => {filterLambda}).map({itemVar} => {{");
                    result.AddRange(ProcessControlFlowSyntax(_flows[flowName]));
                    result.Add("})}}");
                }
            }

            return result;
        }

        private List<string> ProcessIfSyntax(string line, string flowName)
        {
            var result = new List<string>();

            // Extract condition from if statement
            var conditionMatch = Regex.Match(line.Trim(), @"``@if\s+(\([^)]+\))\s+\{");

            if (conditionMatch.Success && _flows.ContainsKey(flowName))
            {
                var condition = conditionMatch.Groups[1].Value;
                result.Add($"{{{condition} && (");
                result.AddRange(ProcessControlFlowSyntax(_flows[flowName]));
                result.Add(")}}");
            }
            else
            {
                // Handle case where no explicit condition is provided
                // The flow name itself can be treated as a boolean variable
                result.Add($"{{{flowName} && (");
                result.AddRange(ProcessControlFlowSyntax(_flows[flowName]));
                result.Add(")}}");
            }

            return result;
        }

        private List<string> ProcessElseSyntax(string line, string flowName)
        {
            var result = new List<string>();

            if (_flows.ContainsKey(flowName))
            {
                // In JSX, we'll need to use a different approach since there's no direct 'else'
                // This would be used in conjunction with a conditional render from an 'if'
                result.Add("{!condition && ("); // Note: 'condition' would need to be defined elsewhere
                result.AddRange(ProcessControlFlowSyntax(_flows[flowName]));
                result.Add(")}}");
            }

            return result;
        }

        private List<string> ProcessIfWithExprSyntax(string expression, string flowName)
        {
            var result = new List<string>();

            if (_flows.ContainsKey(flowName))
            {
                result.Add($"{{{expression.TrimStart('(').TrimEnd(')')} && (");
                result.AddRange(ProcessControlFlowSyntax(_flows[flowName]));
                result.Add(")}}");
            }
            else
            {
                // If flow is not found, add a comment
                result.Add($"{{/* Flow '{flowName}' not found */}}");
                result.Add($"{{{expression.TrimStart('(').TrimEnd(')')} && null}}");
            }

            return result;
        }
    }
}
