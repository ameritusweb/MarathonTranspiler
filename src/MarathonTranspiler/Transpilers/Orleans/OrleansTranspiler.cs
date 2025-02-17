using MarathonTranspiler.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarathonTranspiler.Extensions;

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
            var annotation = block.Annotations[0];
            var type = annotation.Values.First(v => v.Key == "type").Value;
            var propertyName = block.Code[0].Split('=')[0].Replace("this.", "").Trim();
            var initialValue = block.Code[0].Split('=')[1].Trim().Replace(";", "");

            if (annotation.Values.Any(v => v.Key == "stateName"))
            {
                var stateName = annotation.Values.First(v => v.Key == "stateName").Value;
                var stateId = annotation.Values.FirstOrDefault(v => v.Key == "stateId").Value;

                currentClass.Properties.Add(new TranspiledProperty
                {
                    Name = propertyName,
                    Type = type,
                    StateName = stateName,
                    StateId = stateId,
                    Code = !block.Code[0].StartsWith("this.") ? block.Code[0].Trim() : null,
                });

                if (block.Code[0].StartsWith("this."))
                {
                    // Add initialization to constructor
                    currentClass.ConstructorLines.Add($"this.{propertyName} = {initialValue};");
                }
            }
            else if (!block.Code[0].StartsWith("this."))
            {
                currentClass.Fields.Add(block.Code[0]);
            }
            else
            {
                currentClass.Properties.Add(new TranspiledProperty
                {
                    Name = propertyName,
                    Type = type
                });
                currentClass.ConstructorLines.Add(block.Code[0]);
            }
        }

        protected override void ProcessRun(TranspiledClass currentClass, AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var functionName = annotation.Values.First(v => v.Key == "functionName").Value;
            var isAutomatic = annotation.Values.First(v => v.Key == "isAutomatic").Value == "true";
            var method = GetOrCreateMethod(currentClass, functionName);

            // Set method properties
            method.ReturnType = annotation.Values.GetValue("returnType", "void");
            method.Modifier = annotation.Values.GetValue("modifier", "public");

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

            // Store code by ID if present
            if (annotation.Values.Any(v => v.Key == "id"))
            {
                var id = annotation.Values.First(v => v.Key == "id").Value;
                if (!method.CodeById.ContainsKey(id))
                    method.CodeById[id] = new List<string>();
                method.CodeById[id].AddRange(block.Code);
            }

            if (!isAutomatic)
            {
                // Add test step for method call
                var parameters = block.Annotations.Skip(1)
                    .Where(a => a.Name == "parameter")
                    .Select(a => a.Values.First(v => v.Key == "value").Value);

                var methodCall = parameters.Any()
                    ? $"await grain.{functionName}({string.Join(", ", parameters)});"
                    : $"await grain.{functionName}();";

                currentClass.TestSteps.Add(new TestStep { Code = methodCall, IsAssertion = false });
            }
        }

        protected override void ProcessMore(TranspiledClass currentClass, AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var id = annotation.Values.First(v => v.Key == "id").Value;

            // Find method containing this id
            var method = currentClass.Methods.FirstOrDefault(m => m.CodeById.ContainsKey(id));
            if (method != null)
            {
                if (block.Annotations.Any(a => a.Name == "condition"))
                {
                    var expression = block.Annotations.First(a => a.Name == "condition")
                                                    .Values.First(v => v.Key == "expression").Value;
                    method.CodeById[id].Add($"if ({expression})");
                    method.CodeById[id].Add("{");
                    method.CodeById[id].AddRange(block.Code.Select(line => $"\t{line}"));
                    method.CodeById[id].Add("}");
                }
                else
                {
                    method.CodeById[id].AddRange(block.Code);
                }
            }
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
                    if (!string.IsNullOrEmpty(prop.StateName))
                    {
                        // Generate property that accesses state
                        sb.AppendLine($"\tpublic {prop.Type} {prop.Name}");
                        sb.AppendLine("\t{");
                        sb.AppendLine($"\t\tget => State.{prop.StateName};");
                        sb.AppendLine($"\t\tset => State.{prop.StateName} = value;");
                        sb.AppendLine("\t}");
                    }
                    else
                    {
                        // Regular property
                        var propAttribute = _config.Stateful ? "[JsonProperty]" : "";
                        if (!string.IsNullOrEmpty(propAttribute))
                        {
                            sb.AppendLine($"\t{propAttribute}");
                        }
                        sb.AppendLine($"\tpublic {prop.Type} {prop.Name} {{ get; set; }}");
                    }
                }
                if (classInfo.Properties.Any()) sb.AppendLine();

                // Add state field if stateful
                if (_config.Stateful)
                {
                    sb.AppendLine($"\tprivate IPersistentState<{classInfo.ClassName}State> _state;");
                    sb.AppendLine();
                }

                // Constructor with injections and state
                if (_config.Stateful || classInfo.Injections.Any())
                {
                    sb.AppendLine($"\tpublic {classInfo.ClassName}(");

                    var parameters = new List<string>();

                    // Add injected dependencies first
                    foreach (var injection in classInfo.Injections)
                    {
                        parameters.Add($"\t\t{injection.Type} {injection.ParameterName}");
                    }

                    // Add state if stateful
                    if (_config.Stateful)
                    {
                        parameters.Add($"\t\t[PersistentState(\"{classInfo.ClassName}\")] IPersistentState<{classInfo.ClassName}State> state");
                    }

                    sb.AppendLine(string.Join(",\n", parameters));
                    sb.AppendLine("\t)");
                    sb.AppendLine("\t{");

                    // Initialize injected fields
                    foreach (var injection in classInfo.Injections)
                    {
                        sb.AppendLine($"\t\tthis.{injection.Name} = {injection.ParameterName};");
                    }

                    // Initialize state if stateful
                    if (_config.Stateful)
                    {
                        sb.AppendLine("\t\t_state = state;");
                    }

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
                    sb.AppendLine($"\tpublic async Task {method.Name}({parameters}) {{");
                    if (method.CodeById.Keys.Any())
                    {
                        foreach (var code in method.CodeById.Values)
                        {
                            foreach (var line in code)
                            {
                                sb.AppendLine($"\t\t{line}");
                            }
                        }
                    }
                    else
                    {
                        foreach (var line in method.Code)
                        {
                            sb.AppendLine($"\t\t{line}");
                        }
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
                    if (!string.IsNullOrEmpty(_config.StorageProvider))
                    {
                        sb.AppendLine($"[StorageProvider(ProviderName = \"{_config.StorageProvider}\")]");
                    }
                    sb.AppendLine("[GenerateSerializer]");
                    sb.AppendLine($"public class {classInfo.ClassName}State {{");
                    foreach (var prop in classInfo.Properties.Where(p => !string.IsNullOrEmpty(p.StateName)))
                    {
                        sb.AppendLine($"\t[JsonProperty]");
                        if (!string.IsNullOrEmpty(prop.StateId))
                        {
                            sb.AppendLine($"\t[Id({prop.StateId})]");
                        }

                        if (prop.Code != null)
                        {
                            sb.AppendLine($"\t" + prop.Code);
                        }
                        else
                        {
                            sb.AppendLine($"\tpublic {prop.Type} {prop.StateName} {{ get; set; }}");
                        }
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

            // Generate Program class
            sb.AppendLine("public class Program {");
            sb.AppendLine("\tpublic static async Task Main(string[] args) {");
            sb.AppendLine("\t\tvar client = new ClientBuilder()");
            sb.AppendLine("\t\t\t.UseLocalhostClustering()");
            sb.AppendLine("\t\t\t.Build();");
            sb.AppendLine("\t\tawait client.Connect();");

            foreach (var className in _classes.Keys)
            {
                var grainType = $"I{className}";
                var grainInstance = char.ToLower(className[0]) + className.Substring(1);
                sb.AppendLine($"\t\tvar {grainInstance} = client.GetGrain<{grainType}>(Guid.NewGuid());");
            }

            foreach (var line in _mainMethodLines)
            {
                sb.AppendLine($"\t\t{line}");
            }

            sb.AppendLine("\t}");
            sb.AppendLine("}");

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

        protected override void ProcessInject(TranspiledClass currentClass, AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var type = annotation.Values.First(v => v.Key == "type").Value;
            var name = annotation.Values.First(v => v.Key == "name").Value;

            currentClass.Injections.Add(new InjectedDependency
            {
                Type = type,
                Name = name
            });
            currentClass.Fields.Add($"private readonly {type} {name};");
        }
    }
}
