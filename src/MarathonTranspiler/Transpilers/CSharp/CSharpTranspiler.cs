using MarathonTranspiler.Core;
using MarathonTranspiler.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.CSharp
{
    public partial class CSharpTranspiler : MarathonTranspilerBase
    {
        private readonly CSharpConfig _config;

        public CSharpTranspiler(CSharpConfig config)
        {
            _config = config;
        }

        public override string GenerateOutput()
        {
            var sb = new StringBuilder();

            // Add test framework import if there are any assertions
            if (_classes.Values.Any(c => c.Assertions.Any()))
            {
                sb.AppendLine(GetTestImport());
                sb.AppendLine();
            }

            // Generate the main class definitions
            foreach (var classInfo in _classes.Values)
            {
                // Main class
                sb.AppendLine($"public class {classInfo.ClassName} {{");

                // Fields
                foreach (var field in classInfo.Fields)
                {
                    sb.AppendLine($"\t{field}");
                }
                if (classInfo.Fields.Any()) sb.AppendLine();

                // Properties
                foreach (var prop in classInfo.Properties)
                {
                    sb.AppendLine($"\tpublic {prop.Type} {prop.Name} {{ get; set; }}");
                }
                if (classInfo.Properties.Any()) sb.AppendLine();

                // Constructor
                if (classInfo.ConstructorLines.Any())
                {
                    sb.AppendLine($"\tpublic {classInfo.ClassName}() {{");
                    foreach (var line in classInfo.ConstructorLines)
                    {
                        sb.AppendLine($"\t\t{line}");
                    }
                    sb.AppendLine("\t}");
                    sb.AppendLine();
                }

                // Methods
                foreach (var method in classInfo.Methods)
                {
                    var parameters = string.Join(", ", method.Parameters);
                    sb.AppendLine($"\tpublic void {method.Name}({parameters}) {{");
                    foreach (var line in method.Code)
                    {
                        sb.AppendLine($"\t\t{line}");
                    }
                    sb.AppendLine("\t}");
                    sb.AppendLine();
                }

                sb.AppendLine("}");
                sb.AppendLine();
            }

            // Generate test classes for classes with assertions
            sb.Append(GenerateTests());

            // Program class
            sb.AppendLine("public class Program {");
            sb.AppendLine("\tpublic static void Main(string[] args) {");

            foreach (var className in _classes.Keys)
            {
                var instanceName = char.ToLower(className[0]) + className.Substring(1);
                sb.AppendLine($"\t\t{className} {instanceName} = new {className}();");
            }

            foreach (var line in _mainMethodLines)
            {
                sb.AppendLine($"\t\t{line}");
            }

            sb.AppendLine("\t}");
            sb.AppendLine("}");

            return sb.ToString();
        }

        // Enhanced test generation to handle nested class definitions
        private string GenerateTests()
        {
            var sb = new StringBuilder();
            bool hasTests = false;

            foreach (var classInfo in _classes.Values.Where(c => c.Assertions.Any()))
            {
                hasTests = true;

                sb.AppendLine($"public class {classInfo.ClassName}Tests {{");
                sb.AppendLine($"\t{GetTestAttribute()}");
                sb.AppendLine($"\tpublic void TestAssertions() {{");

                // Create the class under test
                sb.AppendLine($"\t\tvar instance = new {classInfo.ClassName}();");

                // Create any dependent classes that might be needed
                foreach (var assertion in classInfo.Assertions)
                {
                    // Check if the assertion references any property types that are custom classes we need to create
                    foreach (var prop in classInfo.Properties)
                    {
                        if (_classes.ContainsKey(prop.Type) && !IsBuiltInType(prop.Type))
                        {
                            sb.AppendLine($"\t\t// Setup {prop.Type} instance for testing");
                            sb.AppendLine($"\t\tvar {prop.Type.ToLower()}Instance = new {prop.Type}();");
                            sb.AppendLine($"\t\tinstance.{prop.Name} = {prop.Type.ToLower()}Instance;");
                        }
                    }
                }

                // Add the assertions
                foreach (var assertion in classInfo.Assertions)
                {
                    sb.AppendLine($"\t\t{assertion}");
                }

                sb.AppendLine("\t}");
                sb.AppendLine("}");
                sb.AppendLine();
            }

            return hasTests ? sb.ToString() : string.Empty;
        }

        // Helper to check if a type is a built-in C# type
        private bool IsBuiltInType(string typeName)
        {
            var builtInTypes = new[]
            {
                "string", "int", "bool", "double", "float", "decimal", "long", "short",
                "byte", "char", "object", "dynamic", "void", "DateTime", "TimeSpan",
                "Guid", "Uri", "Task", "List", "Dictionary", "HashSet", "IEnumerable"
            };

            return builtInTypes.Any(t => typeName.StartsWith(t));
        }

        private string GetTestAttribute()
        {
            return _config.TestFramework.ToLower() switch
            {
                "nunit" => "[Test]",
                _ => "[Fact]" // xunit default
            };
        }

        private string GetTestImport()
        {
            return _config.TestFramework.ToLower() switch
            {
                "nunit" => "using NUnit.Framework;",
                _ => "using Xunit;" // xunit default
            };
        }
    }
}
