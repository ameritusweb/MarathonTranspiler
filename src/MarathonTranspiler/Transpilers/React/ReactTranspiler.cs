using MarathonTranspiler.Core;
using MarathonTranspiler.Helpers;
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
        private readonly StringBuilder _jsxOutput = new();
        private readonly HashSet<string> _imports = new() { "import React from 'react';" };
        private readonly Dictionary<string, List<string>> _customHooks = new();
        private readonly Dictionary<string, List<string>> _testCases = new();
        private readonly ReactConfig _config;
        private readonly ReactTestGenerator _testGenerator;

        public ReactTranspiler(ReactConfig config)
        {
            this._config = config;
            this._testGenerator = new ReactTestGenerator(config);

            // Add default hooks based on configuration
            if (config.IncludedHooks != null)
            {
                foreach (var hook in config.IncludedHooks)
                {
                    _imports.Add($"import {{ {hook} }} from 'react';");
                }
            }
        }

        public override string GenerateOutput()
        {
            var sb = new StringBuilder();

            // Add imports
            foreach (var import in _imports)
            {
                sb.AppendLine(import);
            }
            sb.AppendLine();

            // Generate custom hooks
            foreach (var hook in _customHooks)
            {
                sb.AppendLine($"function {hook.Key}() {{");
                foreach (var line in hook.Value)
                {
                    sb.AppendLine($"    {line}");
                }
                sb.AppendLine("}");
                sb.AppendLine();
            }

            // Generate components
            foreach (var classInfo in _classes.Values)
            {
                sb.AppendLine($"function {classInfo.ClassName}() {{");

                // State and handlers
                foreach (var line in _mainMethodLines)
                {
                    sb.AppendLine($"    {line}");
                }
                sb.AppendLine();

                // JSX
                sb.AppendLine("    return (");
                sb.AppendLine("        <div>");
                sb.Append(_jsxOutput);
                sb.AppendLine("        </div>");
                sb.AppendLine("    );");
                sb.AppendLine("}");

                sb.AppendLine($"export default {classInfo.ClassName};");
                sb.AppendLine();
            }

            // Generate test files if there are assertions
            if (_classes.Values.Any(c => c.Assertions.Any() ||
                (c.AdditionalData.ContainsKey("assertions") && c.AdditionalData["assertions"].Any())))
            {
                // Use the dedicated test generator to create test code
                string testCode = _testGenerator.GenerateTestSuite(_classes);

                // Save the test file
                string testFileName = $"{_config.Name ?? "App"}Tests.js";
                File.WriteAllText(testFileName, testCode);
            }

            return sb.ToString();
        }

        // Helper method to add an assertion to a component
        protected void AddAssertionToClass(string className, string condition, string message)
        {
            if (!_classes.ContainsKey(className))
            {
                _classes[className] = new TranspiledClass { ClassName = className };
            }

            var currentClass = _classes[className];

            // Store the assertion in the additional data 
            if (!currentClass.AdditionalData.ContainsKey("assertions"))
            {
                currentClass.AdditionalData["assertions"] = new List<string>();
            }

            currentClass.AdditionalData["assertions"].Add($"{condition} => \"{message}\"");
        }

        // Helper method to add a test case - using the test generator
        private void AddTestCase(string componentName, string condition, string message)
        {
            if (!_testCases.ContainsKey(componentName))
            {
                _testCases[componentName] = new List<string>();
            }

            // Format the expectation using the shared helper
            string expectation = AssertionHelper.FormatConditionForTestFramework(condition, _config.TestFramework);

            _testCases[componentName].Add($"test('{message}', () => {{");
            _testCases[componentName].Add($"    render(<{componentName} />);");
            _testCases[componentName].Add($"    {expectation}");
            _testCases[componentName].Add("});");
        }
    }
}