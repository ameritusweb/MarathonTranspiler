using MarathonTranspiler.Core;
using MarathonTranspiler.Extensions;
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

        protected override void ProcessVarInit(TranspiledClass currentClass, AnnotatedCode block)
        {
            if (!block.Code[0].StartsWith("this."))
            {
                currentClass.Fields.Add(block.Code[0]);
            }
            else
            {
                var type = block.Annotations[0].Values.First(v => v.Key == "type").Value;
                var propertyName = block.Code[0].Split('=')[0].Replace("this.", "").Trim();
                currentClass.Properties.Add(new TranspiledProperty { Name = propertyName, Type = type });
                currentClass.ConstructorLines.Add(block.Code[0]);
            }
        }

        protected override void ProcessRun(TranspiledClass currentClass, AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var functionName = annotation.Values.First(v => v.Key == "functionName").Value;
            var method = GetOrCreateMethod(currentClass, functionName);

            // Handle parameters
            foreach (var paramAnnotation in block.Annotations.Skip(1))
            {
                if (paramAnnotation.Name == "parameter")
                {
                    var paramType = paramAnnotation.Values.First(v => v.Key == "type").Value;
                    var paramName = paramAnnotation.Values.First(v => v.Key == "name").Value;
                    var param = $"{paramType} {paramName}";
                    if (!method.Parameters.Contains(param))
                    {
                        method.Parameters.Add(param);
                    }
                }
            }

            method.Id = annotation.Values.GetValue("id");
            method.Code.AddRange(block.Code);

            if (!annotation.Values.Any(v => v.Key == "enumerableStart" || v.Key == "enumerableEnd"))
            {
                var paramValues = block.Annotations.Skip(1)
                    .Where(a => a.Name == "parameter")
                    .Select(a => a.Values.First(v => v.Key == "value").Value);

                var instanceName = char.ToLower(currentClass.ClassName[0]) + currentClass.ClassName.Substring(1);
                _mainMethodLines.Add($"{instanceName}.{functionName}({string.Join(", ", paramValues)});");
            }
        }

        protected override void ProcessMore(TranspiledClass currentClass, AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var id = annotation.Values.First(v => v.Key == "id").Value;
            var method = currentClass.Methods.FirstOrDefault(m => m.Id == id);
            int insertIndex = -1;

            if (method == null)
            {
                method = currentClass.Methods.FirstOrDefault(x => x.IndexById.ContainsKey(id));
                if (method != null)
                {
                    insertIndex = method.IndexById[id];
                }
            }
            else
            {
                insertIndex = method.Code.Count;
            }

            if (method != null && insertIndex != -1)
            {
                if (block.Annotations.Any(a => a.Name == "condition"))
                {
                    var conditionAnnotation = block.Annotations.First(x => x.Name == "condition");
                    var expression = conditionAnnotation.Values.First(v => v.Key == "expression").Value;
                    var conditionId = conditionAnnotation.Values.GetValue("id");

                    List<string> cblock = new List<string>();
                    cblock.Add($"if ({expression})");
                    cblock.Add("{");
                    cblock.AddRange(block.Code.Select(line => $"\t{line}"));
                    cblock.Add("}");

                    // Calculate how many lines we're about to insert
                    int insertedLines = cblock.Count;

                    // Adjust all subsequent indexes
                    foreach (var kvp in method.IndexById.ToList())
                    {
                        if (kvp.Value >= insertIndex)
                        {
                            method.IndexById[kvp.Key] += insertedLines;
                        }
                    }

                    method.Code.InsertRange(insertIndex, cblock);

                    if (!string.IsNullOrEmpty(conditionId))
                    {
                        method.IndexById[conditionId] = insertIndex + insertedLines - 1;
                    }
                }
                else
                {
                    // Adjust indexes for non-conditional inserts too
                    int insertedLines = block.Code.Count;
                    foreach (var kvp in method.IndexById.ToList())
                    {
                        if (kvp.Value >= insertIndex)
                        {
                            method.IndexById[kvp.Key] += insertedLines;
                        }
                    }

                    method.Code.InsertRange(insertIndex, block.Code);
                }
            }
        }

        protected override void ProcessAssert(TranspiledClass currentClass, AnnotatedCode block)
        {
            var condition = block.Annotations[0].Values.First(v => v.Key == "condition").Value;
            var message = block.Code[0].Trim('"');
            string assertLine = _config.TestFramework.ToLower() switch
            {
                "nunit" => $"Assert.That({condition}, \"{message}\");",
                _ => $"Assert.True({condition}, \"{message}\");"
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
