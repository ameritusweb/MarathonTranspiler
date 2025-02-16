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

        public CSharpTranspiler(CSharpConfig config)
        {
            _config = config;
        }

        protected override void ProcessBlock(AnnotatedCode block)
        {
            var mainAnnotation = block.Annotations[0];
            var className = mainAnnotation.Values.First(v => v.Key == "className").Value;

            if (!_classes.ContainsKey(className))
            {
                _classes[className] = new TranspiledClass { ClassName = className };
            }

            var currentClass = _classes[className];

            switch (mainAnnotation.Name)
            {
                case "varInit":
                    if (!block.Code[0].StartsWith("this."))
                    {
                        currentClass.Fields.Add(block.Code[0]);
                    }
                    else
                    {
                        var type = mainAnnotation.Values.First(v => v.Key == "type").Value;
                        var propertyName = block.Code[0].Split('=')[0].Replace("this.", "").Trim();
                        currentClass.Properties.Add(new TranspiledProperty { Name = propertyName, Type = type });
                        currentClass.ConstructorLines.Add(block.Code[0]);
                    }
                    break;

                case "assert":
                    ProcessAssert(currentClass, block);
                    break;

                case "run":
                    var functionName = mainAnnotation.Values.First(v => v.Key == "functionName").Value;
                    var method = GetOrCreateMethod(currentClass, functionName);

                    foreach (var annotation in block.Annotations.Skip(1))
                    {
                        if (annotation.Name == "parameter")
                        {
                            var param = $"{annotation.Values.First(v => v.Key == "type").Value} {annotation.Values.First(v => v.Key == "name").Value}";
                            if (!method.Parameters.Contains(param))
                            {
                                method.Parameters.Add(param);
                            }
                        }
                    }

                    method.Code.AddRange(block.Code);

                    if (!mainAnnotation.Values.Any(v => v.Key == "enumerableStart" || v.Key == "enumerableEnd"))
                    {
                        var paramValues = block.Annotations.Skip(1)
                            .Where(a => a.Name == "parameter")
                            .Select(a => a.Values.First(v => v.Key == "value").Value);

                        var instanceName = char.ToLower(className[0]) + className.Substring(1);
                        _mainMethodLines.Add($"{instanceName}.{functionName}({string.Join(", ", paramValues)});");
                    }
                    break;
            }
        }

        protected virtual void ProcessAssert(TranspiledClass currentClass, AnnotatedCode block)
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

        private TranspiledMethod GetOrCreateMethod(TranspiledClass currentClass, string methodName)
        {
            var method = currentClass.Methods.FirstOrDefault(m => m.Name == methodName);
            if (method == null)
            {
                method = new TranspiledMethod { Name = methodName };
                currentClass.Methods.Add(method);
            }
            return method;
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
    }
}
