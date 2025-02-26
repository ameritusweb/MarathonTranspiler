using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Core
{
    public class FlowValidator
    {
        /// <summary>
        /// Validates that all flow references in control flow syntax have corresponding flow definitions
        /// </summary>
        /// <param name="annotatedCodes">The annotated code blocks to validate</param>
        /// <returns>A list of validation errors, or an empty list if validation passes</returns>
        public static List<string> ValidateFlowReferences(List<AnnotatedCode> annotatedCodes)
        {
            var errors = new List<string>();

            // First, collect all defined flow names
            var definedFlows = new HashSet<string>();
            var flowDefinitionLines = new Dictionary<string, int>();
            var lineCounter = 0;

            foreach (var block in annotatedCodes)
            {
                if (block.Annotations.Count > 0 && block.Annotations[0].Name == "flow")
                {
                    var flowName = block.Annotations[0].Values
                        .FirstOrDefault(v => v.Key == "name")
                        .Value;

                    if (!string.IsNullOrEmpty(flowName))
                    {
                        definedFlows.Add(flowName);
                        flowDefinitionLines[flowName] = lineCounter;
                    }
                }

                // Increment line counter for each block (approximation for error reporting)
                lineCounter += block.Code.Count + block.Annotations.Count;
            }

            // Then, validate that all referenced flows are defined
            lineCounter = 0;
            foreach (var block in annotatedCodes)
            {
                // Check both control flow syntax and direct flow references
                if (block.ContainsControlFlow() || block.ContainsDirectFlowReferences())
                {
                    var flowReferences = block.ExtractFlowReferences();
                    var directReferences = block.ExtractDirectFlowReferences();

                    // Combine all references
                    var allReferences = new HashSet<string>(flowReferences.Concat(directReferences));

                    foreach (var reference in allReferences)
                    {
                        if (!definedFlows.Contains(reference))
                        {
                            var blockInfo = "";
                            if (block.Annotations.Count > 0)
                            {
                                var mainAnnotation = block.Annotations[0];
                                if (mainAnnotation.Name == "run" &&
                                    mainAnnotation.Values.Any(v => v.Key == "className") &&
                                    mainAnnotation.Values.Any(v => v.Key == "functionName"))
                                {
                                    var className = mainAnnotation.Values.First(v => v.Key == "className").Value;
                                    var funcName = mainAnnotation.Values.First(v => v.Key == "functionName").Value;
                                    blockInfo = $" in {className}.{funcName}()";
                                }
                            }

                            errors.Add($"Error: Flow reference '{reference}'{blockInfo} is used but not defined in any @flow annotation.");

                            // Suggest possible matches (typo detection)
                            var suggestions = GetSimilarFlowNames(reference, definedFlows);
                            if (suggestions.Any())
                            {
                                errors.Add($"  Did you mean: {string.Join(", ", suggestions)}?");
                            }
                        }
                    }
                }

                // Increment line counter for each block
                lineCounter += block.Code.Count + block.Annotations.Count;
            }

            return errors;
        }

        /// <summary>
        /// Finds flow names that are similar to the reference, to suggest possible typo corrections
        /// </summary>
        private static IEnumerable<string> GetSimilarFlowNames(string reference, HashSet<string> definedFlows)
        {
            // Simple Levenshtein distance-based suggestion
            var maxDistance = Math.Max(2, reference.Length / 4); // Allow more distance for longer names

            return definedFlows
                .Where(flow => LevenshteinDistance(reference, flow) <= maxDistance)
                .OrderBy(flow => LevenshteinDistance(reference, flow))
                .Take(3); // Limit to 3 suggestions
        }

        /// <summary>
        /// Calculates the Levenshtein distance between two strings
        /// </summary>
        private static int LevenshteinDistance(string a, string b)
        {
            if (string.IsNullOrEmpty(a))
                return string.IsNullOrEmpty(b) ? 0 : b.Length;

            if (string.IsNullOrEmpty(b))
                return a.Length;

            var lenA = a.Length;
            var lenB = b.Length;
            var d = new int[lenA + 1, lenB + 1];

            for (var i = 0; i <= lenA; i++)
                d[i, 0] = i;

            for (var j = 0; j <= lenB; j++)
                d[0, j] = j;

            for (var i = 1; i <= lenA; i++)
            {
                for (var j = 1; j <= lenB; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;

                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[lenA, lenB];
        }
    }
}