using MarathonTranspiler.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.Orleans
{
    public class OrleansTranspiler : MarathonTranspilerBase
    {
        private readonly OrleansConfig _config;

        public OrleansTranspiler(OrleansConfig config)
        {
            _config = config;
        }

        private string GetGrainInterface(string className)
        {
            if (!_config.GrainKeyTypes.TryGetValue(className, out var keyType))
                keyType = "string";

            return keyType.ToLower() switch
            {
                "guid" => "IGrainWithGuidKey",
                "long" => "IGrainWithIntegerKey",
                _ => "IGrainWithStringKey"
            };
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

            method.Code.AddRange(block.Code);

            // Add test step for method call
            var parameters = block.Annotations.Skip(1)
                .Where(a => a.Name == "parameter")
                .Select(a => a.Values.First(v => v.Key == "value").Value);

            var methodCall = parameters.Any()
                ? $"await grain.{functionName}({string.Join(", ", parameters)});"
                : $"await grain.{functionName}();";

            currentClass.TestSteps.Add(new TestStep { Code = methodCall, IsAssertion = false });
        }

        public override string GenerateOutput()
        {
            var sb = new StringBuilder();

            foreach (var classInfo in _classes.Values)
            {
                var grainInterface = GetGrainInterface(classInfo.ClassName);
                var interfaces = new List<string> { grainInterface };

                // Add stream interfaces if configured
                if (_config.Streams?.ContainsKey(classInfo.ClassName) == true)
                {
                    interfaces.AddRange(_config.Streams[classInfo.ClassName]);
                }

                // Generate interface
                sb.AppendLine($"public interface I{classInfo.ClassName} : {string.Join(", ", interfaces)} {{");
                foreach (var method in classInfo.Methods)
                {
                    var parameters = string.Join(", ", method.Parameters);
                    sb.AppendLine($"\tTask {method.Name}({parameters});");
                }
                sb.AppendLine("}");
                sb.AppendLine();

                // Generate grain class
                var baseClasses = new List<string> { "Grain", $"I{classInfo.ClassName}" };
                if (_config.Stateful)
                {
                    baseClasses.Add("IRemindable");
                }

                sb.AppendLine($"public class {classInfo.ClassName} : {string.Join(", ", baseClasses)} {{");

                // Fields
                foreach (var field in classInfo.Fields)
                {
                    sb.AppendLine($"\t{field}");
                }
                if (classInfo.Fields.Any()) sb.AppendLine();

                // Stream properties if configured
                if (_config.Streams?.ContainsKey(classInfo.ClassName) == true)
                {
                    foreach (var streamType in _config.Streams[classInfo.ClassName])
                    {
                        sb.AppendLine($"\tprivate {streamType} _stream;");
                    }
                    sb.AppendLine();
                }

                // Properties
                foreach (var prop in classInfo.Properties)
                {
                    var propAttribute = _config.Stateful ? "[JsonProperty]" : "";
                    if (!string.IsNullOrEmpty(propAttribute))
                    {
                        sb.AppendLine($"\t{propAttribute}");
                    }
                    sb.AppendLine($"\tpublic {prop.Type} {prop.Name} {{ get; set; }}");
                }
                if (classInfo.Properties.Any()) sb.AppendLine();

                // Add state if stateful
                if (_config.Stateful)
                {
                    sb.AppendLine($"\tprivate IPersistentState<{classInfo.ClassName}State> _state;");
                    sb.AppendLine();
                    sb.AppendLine($"\tpublic {classInfo.ClassName}(");
                    sb.AppendLine($"\t\t[PersistentState(\"{classInfo.ClassName}\")] IPersistentState<{classInfo.ClassName}State> state)");
                    sb.AppendLine("\t{");
                    sb.AppendLine("\t\t_state = state;");
                    sb.AppendLine("\t}");
                    sb.AppendLine();
                }

                // Methods
                foreach (var method in classInfo.Methods)
                {
                    var parameters = string.Join(", ", method.Parameters);
                    sb.AppendLine($"\tpublic async Task {method.Name}({parameters}) {{");
                    foreach (var line in method.Code)
                    {
                        sb.AppendLine($"\t\t{line}");
                    }
                    if (_config.Stateful)
                    {
                        sb.AppendLine("\t\tawait _state.WriteStateAsync();");
                    }
                    sb.AppendLine("\t}");
                    sb.AppendLine();
                }

                // Add IRemindable implementation if stateful
                if (_config.Stateful)
                {
                    sb.AppendLine("\tpublic Task ReceiveReminder(string reminderName, TickStatus status) => Task.CompletedTask;");
                }

                sb.AppendLine("}");

                // Generate state class if stateful
                if (_config.Stateful)
                {
                    sb.AppendLine($"public class {classInfo.ClassName}State {{");
                    foreach (var prop in classInfo.Properties)
                    {
                        sb.AppendLine($"\t[JsonProperty]");
                        sb.AppendLine($"\tpublic {prop.Type} {prop.Name} {{ get; set; }}");
                    }
                    sb.AppendLine("}");
                }

                // After generating the grain and state classes...
                if (classInfo.TestSteps.Any(x => x.IsAssertion))
                {
                    var testAttribute = _config.TestFramework.ToLower() switch
                    {
                        "nunit" => "[Test]",
                        _ => "[Fact]"
                    };

                    sb.AppendLine($"public class {classInfo.ClassName}Tests {{");
                    sb.AppendLine($"\t{testAttribute}");
                    sb.AppendLine($"\tpublic async Task TestAssertions() {{");
                    sb.AppendLine($"\t\tvar grain = await _cluster.CreateGrainAsync<{classInfo.ClassName}>();");

                    foreach (var step in classInfo.TestSteps)
                    {
                        sb.AppendLine($"\t\t{step.Code}");
                    }

                    sb.AppendLine("\t}");
                    sb.AppendLine("}");
                }
            }

            return sb.ToString();
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
            currentClass.TestSteps.Add(new TestStep { Code = assertLine, IsAssertion = true });
        }
    }
}
