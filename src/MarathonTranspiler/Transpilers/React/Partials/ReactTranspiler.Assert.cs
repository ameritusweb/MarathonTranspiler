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
        protected override void ProcessAssert(TranspiledClass currentClass, AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var condition = annotation.Values.First(v => v.Key == "condition").Value;
            var message = block.Code[0].Trim('"');

            // Normalize the class name for consistency
            var className = currentClass.ClassName;

            // Process assertions both for component testing and runtime validation

            // 1. Add to test cases for Jest/React Testing Library
            AddTestCase(className, condition, message);

            // 2. Add runtime validation in the component using React Error Boundaries
            // In React, we'd typically use propTypes, TypeScript, or conditional rendering
            // for validation rather than assertions, but we can add some validation code

            // Add validation code based on the condition
            string validationCode;

            // Handle the "after" attribute for assertions that should run after a specific method
            if (annotation.Values.Any(v => v.Key == "after"))
            {
                var afterMethod = annotation.Values.First(v => v.Key == "after").Value;

                // Add an effect that runs after the method
                if (!_imports.Contains("import { useEffect } from 'react';"))
                {
                    _imports.Add("import { useEffect } from 'react';");
                }

                _mainMethodLines.Add($"// Validation for: {message}");
                _mainMethodLines.Add($"useEffect(() => {{");
                _mainMethodLines.Add($"  if (!({condition})) {{");
                _mainMethodLines.Add($"    console.warn('Assertion failed: {message}');");
                _mainMethodLines.Add($"  }}");
                _mainMethodLines.Add($"}}, [{afterMethod}Dependencies]);  // Dependencies should be properly set");
            }
            else
            {
                // Add general validation code
                _mainMethodLines.Add($"// Validation for: {message}");
                _mainMethodLines.Add($"if (process.env.NODE_ENV !== 'production' && !({condition})) {{");
                _mainMethodLines.Add($"  console.warn('Assertion failed: {message}');");
                _mainMethodLines.Add($"}}");
            }

            // 3. Add to component's stored assertions for enhanced development experience
            if (!currentClass.AdditionalData.ContainsKey("assertions"))
            {
                currentClass.AdditionalData["assertions"] = new List<string>();
            }

            currentClass.AdditionalData["assertions"].Add($"{condition} => \"{message}\"");

            // 4. If using TypeScript, we could also add type assertions in some cases
            if (_config.UseTypeScript && condition.Contains(".") && !condition.Contains("("))
            {
                // This is a simplistic approach - a real implementation would need more robust parsing
                var parts = condition.Split('.');
                if (parts.Length >= 2)
                {
                    var propName = parts[parts.Length - 1];
                    _mainMethodLines.Add($"// TypeScript assertion for {propName}");
                }
            }
        }
    }
}