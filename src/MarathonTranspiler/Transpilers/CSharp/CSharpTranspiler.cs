using MarathonTranspiler.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.CSharp
{
    public class CSharpTranspiler : MarathonTranspilerBase
    {
        private readonly CSharpConfig _config;

        protected override void ProcessAssert(TranspiledClass currentClass, AnnotatedCode block)
        {
            var condition = block.Annotations[0].Values.First(v => v.Key == "condition").Value;
            var message = block.Code[0].Trim('"');

            string assertLine = _config.TestFramework.ToLower() switch
            {
                "nunit" => $"Assert.That({condition}, \"{message}\");",
                _ => $"Assert.True({condition}, \"{message}\");" // xunit default
            };

            currentClass.Assertions.Add(assertLine);
        }

        public override string GenerateOutput()
        {
            var sb = new StringBuilder();

            // Add test framework import
            sb.AppendLine(GetTestImport());
            sb.AppendLine();

            foreach (var classInfo in _classes.Values)
            {
                // Main class generation...

                // Test class
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
