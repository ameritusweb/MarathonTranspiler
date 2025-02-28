using MarathonTranspiler.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MarathonTranspiler.Helpers
{
    /// <summary>
    /// Helper class for flow processing that can be shared across different transpilers
    /// to reduce code duplication while maintaining platform-specific processing.
    /// </summary>
    public class FlowProcessingHelper
    {
        /// <summary>
        /// Identifies if a code line contains direct flow references in the format {flowName}
        /// </summary>
        /// <param name="line">The code line to check</param>
        /// <returns>True if the line contains a direct flow reference</returns>
        public static bool IsDirectFlowReference(string line)
        {
            var trimmed = line.Trim();
            return trimmed.StartsWith("{") && trimmed.EndsWith("}") && !trimmed.Contains(" ");
        }

        /// <summary>
        /// Extracts the flow name from a direct flow reference {flowName}
        /// </summary>
        /// <param name="line">The code line containing the flow reference</param>
        /// <returns>The extracted flow name</returns>
        public static string ExtractDirectFlowName(string line)
        {
            var trimmed = line.Trim();
            if (IsDirectFlowReference(trimmed))
            {
                return trimmed.Substring(1, trimmed.Length - 2);
            }
            return string.Empty;
        }

        /// <summary>
        /// Checks if a line contains a control flow syntax like ``@if {flowName}
        /// </summary>
        /// <param name="line">The code line to check</param>
        /// <returns>True if the line contains control flow syntax</returns>
        public static bool IsControlFlowSyntax(string line)
        {
            return line.TrimStart().StartsWith("``@");
        }

        /// <summary>
        /// Extracts parts of a loop expression [item:collection]
        /// </summary>
        /// <param name="loopExpr">The loop expression to parse</param>
        /// <returns>A tuple containing (item, collection, operation) where operation is optional</returns>
        public static (string item, string collection, string operation) ParseLoopExpression(string loopExpr)
        {
            var parts = loopExpr.Split(':');

            if (parts.Length == 1)
            {
                // Simple collection: [collection]
                return ("item", parts[0].Trim(), string.Empty);
            }
            else if (parts.Length == 2)
            {
                // Either [item:collection] or [i=start:end]
                var firstPart = parts[0].Trim();
                var secondPart = parts[1].Trim();

                if (firstPart.Contains("="))
                {
                    // Range syntax [i=1:10]
                    var rangeMatch = Regex.Match(loopExpr, @"(\w+)=(\d+):(\d+)");
                    if (rangeMatch.Success)
                    {
                        var varName = rangeMatch.Groups[1].Value;
                        var start = rangeMatch.Groups[2].Value;
                        var end = rangeMatch.Groups[3].Value;
                        return (varName, $"{start}:{end}", "range");
                    }
                }

                // Basic iteration: [item:collection]
                return (firstPart, secondPart, string.Empty);
            }
            else if (parts.Length == 3)
            {
                // Filter or transform: [item:operation:collection]
                var itemVar = parts[0].Trim();
                var operation = parts[1].Trim();
                var collection = parts[2].Trim();

                // Determine if this is a filter or transform
                bool isFilter = IsFilterOperation(operation, itemVar);

                return (itemVar, collection, isFilter ? "filter:" + operation : "transform:" + operation);
            }

            // Default case
            return ("item", loopExpr, string.Empty);
        }

        /// <summary>
        /// Determines if an operation is a filter operation (contains comparison operators)
        /// </summary>
        private static bool IsFilterOperation(string operation, string itemVar)
        {
            return operation.Contains(">") || operation.Contains("<") ||
                   operation.Contains("==") || operation.Contains("!=") ||
                   operation.StartsWith("!") || operation.Contains("&&") ||
                   operation.Contains("||");
        }

        /// <summary>
        /// Extracts a condition from an if statement syntax
        /// </summary>
        /// <param name="line">The line containing the if statement</param>
        /// <returns>The extracted condition</returns>
        public static string ExtractCondition(string line)
        {
            var conditionMatch = Regex.Match(line.Trim(), @"``@if\s+(\([^)]+\))\s+\{");
            if (conditionMatch.Success)
            {
                return conditionMatch.Groups[1].Value;
            }
            return string.Empty;
        }

        /// <summary>
        /// Extracts the flow name from a control flow syntax
        /// </summary>
        /// <param name="line">The line containing the control flow syntax</param>
        /// <returns>The extracted flow name</returns>
        public static string ExtractFlowName(string line)
        {
            var flowMatch = Regex.Match(line.Trim(), @"\{([^}]+)\}");
            if (flowMatch.Success)
            {
                return flowMatch.Groups[1].Value;
            }
            return string.Empty;
        }
    }
}