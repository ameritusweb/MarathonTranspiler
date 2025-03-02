using MarathonTranspiler.Core;
using MarathonTranspiler.Extensions;
using MarathonTranspiler.Helpers;
using MarathonTranspiler.Model;
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
        private readonly CSharpTestGenerator _testGenerator;

        public CSharpTranspiler(CSharpConfig config, IStaticMethodRegistry registry)
        {
            _config = config;
            _testGenerator = new CSharpTestGenerator(config);
            _inliningHelper = new StaticMethodInliningHelper(registry, "csharp");
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

            // Add dependencies like using statements
            if (_classes.Values.Any(c => c.AdditionalData.ContainsKey("usings")))
            {
                foreach (var classInfo in _classes.Values)
                {
                    if (classInfo.AdditionalData.ContainsKey("usings"))
                    {
                        foreach (var usingStatement in (List<string>)classInfo.AdditionalData["usings"])
                        {
                            sb.AppendLine(usingStatement);
                        }
                    }
                }
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

            // Generate test classes for classes with assertions using the test generator
            if (_classes.Values.Any(c => c.Assertions.Any()))
            {
                string testCode = _testGenerator.GenerateTestSuite(_classes);
                sb.AppendLine(testCode);
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