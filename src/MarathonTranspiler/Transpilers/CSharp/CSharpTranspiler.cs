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

                // Test class if there are assertions
                if (classInfo.Assertions.Any())
                {
                    sb.AppendLine($"public class {classInfo.ClassName}Tests {{");
                    sb.AppendLine($"\t{GetTestAttribute()}");
                    sb.AppendLine($"\tpublic void TestAssertions() {{");
                    sb.AppendLine($"\t\tvar instance = new {classInfo.ClassName}();");
                    foreach (var assertion in classInfo.Assertions)
                    {
                        sb.AppendLine($"\t\t{assertion}");
                    }
                    sb.AppendLine("\t}");
                    sb.AppendLine("}");
                }
            }

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
