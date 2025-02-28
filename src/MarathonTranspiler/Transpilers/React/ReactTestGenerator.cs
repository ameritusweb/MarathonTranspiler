using MarathonTranspiler.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.React
{
    /// <summary>
    /// Generates unit tests for React components based on assertions
    /// </summary>
    public class ReactTestGenerator
    {
        private readonly ReactConfig _config;

        public ReactTestGenerator(ReactConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Generates a complete test suite for a collection of components
        /// </summary>
        public string GenerateTestSuite(Dictionary<string, TranspiledClass> components)
        {
            var sb = new StringBuilder();

            // Add imports
            AddTestImports(sb);

            // Import components
            foreach (var component in components.Values)
            {
                sb.AppendLine($"import {component.ClassName} from './{component.ClassName}';");
            }
            sb.AppendLine();

            // Generate test cases for each component with assertions
            foreach (var component in components.Values.Where(c => c.Assertions.Any()))
            {
                GenerateComponentTests(sb, component);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Adds test framework imports based on configuration
        /// </summary>
        private void AddTestImports(StringBuilder sb)
        {
            sb.AppendLine("import React from 'react';");

            switch (_config.TestFramework.ToLower())
            {
                case "testing-library":
                    sb.AppendLine("import { render, screen, fireEvent } from '@testing-library/react';");
                    break;

                case "enzyme":
                    sb.AppendLine("import { shallow, mount } from 'enzyme';");
                    break;

                case "jest":
                default:
                    sb.AppendLine("import { render, screen, fireEvent } from '@testing-library/react';");
                    break;
            }

            sb.AppendLine();
        }

        /// <summary>
        /// Generates test cases for a single component
        /// </summary>
        private void GenerateComponentTests(StringBuilder sb, TranspiledClass component)
        {
            sb.AppendLine($"describe('{component.ClassName}', () => {{");

            // Generate individual test cases for each assertion
            foreach (var assertion in component.Assertions)
            {
                // Extract message from assertion line
                var message = ExtractAssertionMessage(assertion);

                // Extract condition from assertion line
                var condition = ExtractAssertionCondition(assertion);

                // Convert the condition to a testable expectation
                var expectation = ConvertToExpectation(condition);

                // Generate the test case
                sb.AppendLine($"  test('{message}', () => {{");

                // Determine the test approach based on framework
                switch (_config.TestFramework.ToLower())
                {
                    case "testing-library":
                        sb.AppendLine($"    render(<{component.ClassName} />);");
                        sb.AppendLine($"    {expectation}");
                        break;

                    case "enzyme":
                        sb.AppendLine($"    const wrapper = shallow(<{component.ClassName} />);");
                        sb.AppendLine($"    {expectation.Replace("screen", "wrapper")}");
                        break;

                    case "jest":
                    default:
                        sb.AppendLine($"    render(<{component.ClassName} />);");
                        sb.AppendLine($"    {expectation}");
                        break;
                }

                sb.AppendLine("  });");
                sb.AppendLine();
            }

            sb.AppendLine("});");
            sb.AppendLine();
        }

        /// <summary>
        /// Extracts the message from an assertion line like: Assert.True(condition, "message");
        /// </summary>
        private string ExtractAssertionMessage(string assertion)
        {
            var messageMatch = Regex.Match(assertion, @"""([^""]+)""");
            return messageMatch.Success ? messageMatch.Groups[1].Value : "Assertion should pass";
        }

        /// <summary>
        /// Extracts the condition from an assertion line
        /// </summary>
        private string ExtractAssertionCondition(string assertion)
        {
            var conditionMatch = Regex.Match(assertion, @"Assert\.\w+\(([^,]+)");
            return conditionMatch.Success ? conditionMatch.Groups[1].Value.Trim() : string.Empty;
        }

        /// <summary>
        /// Converts a C# assertion condition to a Jest/RTL expectation
        /// </summary>
        private string ConvertToExpectation(string condition)
        {
            // This is a simplified conversion, would need more robust parsing for complex conditions
            if (condition.Contains("===") || condition.Contains("=="))
            {
                var parts = condition.Split(new[] { "===", "==" }, StringSplitOptions.None);
                return $"expect({parts[0].Trim()}).toEqual({parts[1].Trim()});";
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
            else if (condition.Contains("Contains"))
            {
                // Handle string.Contains or similar
                var match = Regex.Match(condition, @"(.*?)\.Contains\((.*?)\)");
                if (match.Success)
                {
                    return $"expect({match.Groups[1].Value.Trim()}).toContain({match.Groups[2].Value.Trim()});";
                }
            }
            else if (condition.Contains("!"))
            {
                return $"expect({condition.Replace("!", "")}).toBeFalsy();";
            }

            // Default case
            return $"expect({condition}).toBeTruthy();";
        }
    }
}