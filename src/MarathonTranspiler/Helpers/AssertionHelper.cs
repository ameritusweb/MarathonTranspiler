using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MarathonTranspiler.Helpers
{
    /// <summary>
    /// Helper class for processing assertions across different transpilers
    /// </summary>
    public static class AssertionHelper
    {
        /// <summary>
        /// Extracts the condition from an assertion statement
        /// </summary>
        /// <param name="assertion">The assertion line</param>
        /// <returns>The extracted condition</returns>
        public static string ExtractCondition(string assertion)
        {
            // For C# assertions like Assert.True(condition, "message")
            var csAssertMatch = Regex.Match(assertion, @"Assert\.\w+\(([^,]+),");
            if (csAssertMatch.Success)
            {
                return csAssertMatch.Groups[1].Value.Trim();
            }

            // For NUnit assertions like Assert.That(condition, "message")
            var nunitAssertMatch = Regex.Match(assertion, @"Assert\.That\(([^,]+),");
            if (nunitAssertMatch.Success)
            {
                return nunitAssertMatch.Groups[1].Value.Trim();
            }

            // For manual assertions like if (!condition) throw ...
            var ifThrowMatch = Regex.Match(assertion, @"if\s+\(!([^)]+)\)");
            if (ifThrowMatch.Success)
            {
                return ifThrowMatch.Groups[1].Value.Trim();
            }

            // If we couldn't match any known pattern, return empty string
            return string.Empty;
        }

        /// <summary>
        /// Extracts the message from an assertion statement
        /// </summary>
        /// <param name="assertion">The assertion line</param>
        /// <returns>The extracted message</returns>
        public static string ExtractMessage(string assertion)
        {
            var messageMatch = Regex.Match(assertion, @"""([^""]+)""");
            return messageMatch.Success ? messageMatch.Groups[1].Value : "Assertion should pass";
        }

        /// <summary>
        /// Determines if a condition is checking for equality
        /// </summary>
        /// <param name="condition">The condition to check</param>
        /// <returns>True if the condition is an equality check</returns>
        public static bool IsEqualityCheck(string condition)
        {
            return condition.Contains("==") || condition.Contains("===") ||
                   condition.Contains(".Equals(") || condition.Contains(".equals(");
        }

        /// <summary>
        /// Determines if a condition is checking a range (greater than/less than)
        /// </summary>
        /// <param name="condition">The condition to check</param>
        /// <returns>True if the condition is a range check</returns>
        public static bool IsRangeCheck(string condition)
        {
            return condition.Contains(">") || condition.Contains("<");
        }

        /// <summary>
        /// Determines if a condition is checking for a null/undefined value
        /// </summary>
        /// <param name="condition">The condition to check</param>
        /// <returns>True if the condition is checking for null/undefined</returns>
        public static bool IsNullCheck(string condition)
        {
            return condition.Contains("!= null") || condition.Contains("== null") ||
                   condition.Contains("!== null") || condition.Contains("=== null") ||
                   condition.Contains("!= undefined") || condition.Contains("== undefined");
        }

        /// <summary>
        /// Formats a condition for the specified test framework
        /// </summary>
        /// <param name="condition">The condition to format</param>
        /// <param name="framework">The target test framework</param>
        /// <returns>The formatted condition</returns>
        public static string FormatConditionForTestFramework(string condition, string framework)
        {
            switch (framework.ToLower())
            {
                case "jest":
                case "testing-library":
                    return FormatForJest(condition);

                case "nunit":
                    return FormatForNUnit(condition);

                case "xunit":
                    return FormatForXUnit(condition);

                case "enzyme":
                    return FormatForEnzyme(condition);

                default:
                    return condition;
            }
        }

        private static string FormatForJest(string condition)
        {
            if (IsEqualityCheck(condition))
            {
                // Handle equality checks for Jest
                var parts = SplitByOperator(condition, new[] { "===", "==", ".Equals(", ".equals(" });
                var left = parts.Item1.Trim();
                var right = parts.Item2.Trim().TrimEnd(')');

                return $"expect({left}).toEqual({right});";
            }
            else if (condition.Contains(">"))
            {
                var parts = condition.Split('>');
                return $"expect({parts[0].Trim()}).toBeGreaterThan({parts[1].Trim()});";
            }
            else if (condition.Contains("<"))
            {
                var parts = condition.Split('<');
                return $"expect({parts[0].Trim()}).toBeLessThan({parts[1].Trim()});";
            }
            else if (condition.Contains("!"))
            {
                // Negation check
                var content = condition.Replace("!", "").Trim();
                return $"expect({content}).toBeFalsy();";
            }

            // Default case - truth check
            return $"expect({condition}).toBeTruthy();";
        }

        private static string FormatForNUnit(string condition)
        {
            if (IsEqualityCheck(condition))
            {
                // Handle equality checks for NUnit
                var parts = SplitByOperator(condition, new[] { "===", "==", ".Equals(", ".equals(" });
                var left = parts.Item1.Trim();
                var right = parts.Item2.Trim().TrimEnd(')');

                return $"Assert.That({left}, Is.EqualTo({right}));";
            }
            else if (condition.Contains(">"))
            {
                var parts = condition.Split('>');
                return $"Assert.That({parts[0].Trim()}, Is.GreaterThan({parts[1].Trim()}));";
            }
            else if (condition.Contains("<"))
            {
                var parts = condition.Split('<');
                return $"Assert.That({parts[0].Trim()}, Is.LessThan({parts[1].Trim()}));";
            }
            else if (condition.Contains("!"))
            {
                // Negation check
                var content = condition.Replace("!", "").Trim();
                return $"Assert.That({content}, Is.False);";
            }

            // Default case - truth check
            return $"Assert.That({condition}, Is.True);";
        }

        private static string FormatForXUnit(string condition)
        {
            if (IsEqualityCheck(condition))
            {
                // Handle equality checks for XUnit
                var parts = SplitByOperator(condition, new[] { "===", "==", ".Equals(", ".equals(" });
                var left = parts.Item1.Trim();
                var right = parts.Item2.Trim().TrimEnd(')');

                return $"Assert.Equal({right}, {left});";
            }
            else if (condition.Contains(">"))
            {
                var parts = condition.Split('>');
                var left = parts[0].Trim();
                var right = parts[1].Trim();
                return $"Assert.True({left} > {right});";
            }
            else if (condition.Contains("<"))
            {
                var parts = condition.Split('<');
                var left = parts[0].Trim();
                var right = parts[1].Trim();
                return $"Assert.True({left} < {right});";
            }
            else if (condition.Contains("!"))
            {
                // Negation check
                var content = condition.Replace("!", "").Trim();
                return $"Assert.False({content});";
            }

            // Default case - truth check
            return $"Assert.True({condition});";
        }

        private static string FormatForEnzyme(string condition)
        {
            // Similar to Jest but with some Enzyme-specific considerations
            if (condition.Contains("find(") || condition.Contains("exists"))
            {
                return $"expect(wrapper.{condition}).toBeTruthy();";
            }

            return FormatForJest(condition);
        }

        /// <summary>
        /// Splits a string by the first occurrence of any of the given operators
        /// </summary>
        private static Tuple<string, string> SplitByOperator(string input, string[] operators)
        {
            foreach (var op in operators)
            {
                if (input.Contains(op))
                {
                    var parts = input.Split(new[] { op }, 2, StringSplitOptions.None);
                    return Tuple.Create(parts[0], parts[1]);
                }
            }

            // If no operator found, return the whole string and empty string
            return Tuple.Create(input, string.Empty);
        }
    }
}