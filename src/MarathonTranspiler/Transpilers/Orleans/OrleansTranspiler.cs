using MarathonTranspiler.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarathonTranspiler.Extensions;

namespace MarathonTranspiler.Transpilers.Orleans
{
    public partial class OrleansTranspiler : MarathonTranspilerBase
    {
        private readonly OrleansConfig _config;

        public OrleansTranspiler(OrleansConfig config)
        {
            _config = config;
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

                    foreach (var line in method.Code)
                    {
                        sb.AppendLine($"\t\t{line}");
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
    }
}
