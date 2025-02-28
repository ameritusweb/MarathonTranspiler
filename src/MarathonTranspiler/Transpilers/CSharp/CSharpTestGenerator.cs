using MarathonTranspiler.Helpers;
using MarathonTranspiler.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.CSharp
{
    /// <summary>
    /// Generates unit tests for C# classes based on their assertions
    /// </summary>
    public class CSharpTestGenerator : TestGeneratorBase
    {
        private readonly CSharpConfig _config;

        public CSharpTestGenerator(CSharpConfig config)
            : base(config.TestFramework)
        {
            _config = config;
        }

        /// <summary>
        /// Adds the appropriate imports for the selected test framework
        /// </summary>
        protected override void AddImports(StringBuilder sb)
        {
            switch (TestFramework.ToLower())
            {
                case "nunit":
                    sb.AppendLine("using NUnit.Framework;");
                    sb.AppendLine("using System;");
                    sb.AppendLine("using System.Collections.Generic;");
                    sb.AppendLine();
                    break;

                case "xunit":
                default:
                    sb.AppendLine("using Xunit;");
                    sb.AppendLine("using System;");
                    sb.AppendLine("using System.Collections.Generic;");
                    sb.AppendLine();
                    break;
            }
        }

        /// <summary>
        /// Generates tests for a specific class
        /// </summary>
        protected override void GenerateClassTests(StringBuilder sb, TranspiledClass classInfo)
        {
            // Create the test class
            sb.AppendLine($"public class {classInfo.ClassName}Tests");
            sb.AppendLine("{");

            // Generate test method with all assertions
            GenerateTestMethod(sb, classInfo);

            sb.AppendLine("}");
            sb.AppendLine();
        }

        /// <summary>
        /// Generates a single test case
        /// </summary>
        protected override void GenerateTestCase(StringBuilder sb, string assertion, string message, string className)
        {
            sb.AppendLine($"        // {message}");
            sb.AppendLine($"        {assertion}");
            sb.AppendLine();
        }

        /// <summary>
        /// Generates a test method containing all assertions for a class
        /// </summary>
        private void GenerateTestMethod(StringBuilder sb, TranspiledClass classInfo)
        {
            // Add the appropriate test attribute
            string testAttribute = TestFramework.ToLower() == "nunit" ? "[Test]" : "[Fact]";
            sb.AppendLine($"    {testAttribute}");
            sb.AppendLine($"    public void TestAssertions()");
            sb.AppendLine("    {");

            // Create an instance of the class
            sb.AppendLine($"        var instance = new {classInfo.ClassName}();");
            sb.AppendLine();

            // Setup phase - create any dependencies
            SetupDependencies(sb, classInfo);

            // Add all assertions
            foreach (var assertion in classInfo.Assertions)
            {
                sb.AppendLine($"        {assertion}");
            }

            sb.AppendLine("    }");
        }

        /// <summary>
        /// Sets up any dependencies needed for testing
        /// </summary>
        private void SetupDependencies(StringBuilder sb, TranspiledClass classInfo)
        {
            // Check for property types that are other classes
            foreach (var prop in classInfo.Properties)
            {
                if (!IsBuiltInType(prop.Type) && !string.IsNullOrEmpty(prop.Type))
                {
                    sb.AppendLine($"        // Setup {prop.Type} instance for testing");
                    sb.AppendLine($"        var {prop.Type.ToLower()}Instance = new {prop.Type}();");
                    sb.AppendLine($"        instance.{prop.Name} = {prop.Type.ToLower()}Instance;");
                    sb.AppendLine();
                }
            }
        }

        /// <summary>
        /// Checks if a type is a built-in C# type
        /// </summary>
        private bool IsBuiltInType(string typeName)
        {
            var builtInTypes = new[]
            {
                "string", "int", "bool", "double", "float", "decimal", "long", "short",
                "byte", "char", "object", "dynamic", "void", "DateTime", "TimeSpan",
                "Guid", "Uri", "Task", "List", "Dictionary", "HashSet", "IEnumerable"
            };

            return builtInTypes.Any(t => typeName.StartsWith(t, StringComparison.OrdinalIgnoreCase));
        }
    }
}