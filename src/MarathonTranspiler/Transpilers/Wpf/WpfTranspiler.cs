using MarathonTranspiler.Core;
using MarathonTranspiler.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.Wpf
{
    public class WpfTranspiler : MarathonTranspilerBase
    {
        private readonly HashSet<string> _imports = new()
        {
            "using System.Windows;",
            "using System.Windows.Controls;",
            "using System.Windows.Input;",
            "using System.ComponentModel;",
            "using System.Runtime.CompilerServices;",
            "using Microsoft.Extensions.DependencyInjection;"
        };

        private class SlotContent
        {
            public List<string> Content { get; set; } = new();
            public string ParentContainer { get; set; }
            public Dictionary<string, SlotContent> NestedSlots { get; set; } = new();
        }

        private readonly Dictionary<string, Dictionary<string, SlotContent>> _slots = new();
        private readonly Dictionary<string, List<string>> _commandMethods = new();
        private readonly Dictionary<string, List<string>> _services = new();
        private readonly Dictionary<string, List<string>> _injectedDependencies = new();
        private readonly WpfConfig _config;

        public WpfTranspiler(WpfConfig config)
        {
            this._config = config;
        }

        protected internal override void ProcessBlock(AnnotatedCode block, AnnotatedCode? previousBlock)
        {
            var mainAnnotation = block.Annotations[0];
            var className = mainAnnotation.Values.First(v => v.Key == "className").Value;

            if (!_classes.ContainsKey(className))
            {
                _classes[className] = new TranspiledClass
                {
                    ClassName = className,
                    BaseClass = "INotifyPropertyChanged"
                };
            }

            switch (mainAnnotation.Name)
            {
                case "service":
                    var lifetime = mainAnnotation.Values.FirstOrDefault(v => v.Key == "lifetime").Value ?? "Transient";
                    if (!_services.ContainsKey(className))
                    {
                        _services[className] = new();
                    }
                    _services[className].AddRange(block.Code);
                    break;

                case "viewModel":
                    if (!_classes.ContainsKey(className))
                    {
                        _classes[className] = new TranspiledClass
                        {
                            ClassName = className,
                            BaseClass = "INotifyPropertyChanged"
                        };
                    }
                    // Process any injections
                    foreach (var annotation in block.Annotations.Skip(1))
                    {
                        if (annotation.Name == "inject")
                        {
                            var type = annotation.Values.First(v => v.Key == "type").Value;
                            if (!_injectedDependencies.ContainsKey(className))
                            {
                                _injectedDependencies[className] = new();
                            }
                            _injectedDependencies[className].Add(type);
                        }
                    }
                    break;
                case "uiInit":
                    ProcessUiInit(className, mainAnnotation, block);
                    break;

                case "command":
                    ProcessCommand(className, block);
                    break;

                case "onEvent":
                    ProcessEvent(_classes[className], block);
                    break;

                default:
                    base.ProcessBlock(block, previousBlock);
                    break;
            }
        }

        private void ProcessUiInit(string className, Annotation annotation, AnnotatedCode block)
        {
            if (!_slots.ContainsKey(className))
            {
                _slots[className] = new Dictionary<string, SlotContent>();
            }

            var container = annotation.Values.First(v => v.Key == "container").Value;

            if (annotation.Values.Any(v => v.Key == "slotName"))
            {
                var slotName = annotation.Values.First(v => v.Key == "slotName").Value;
                var slotContent = new SlotContent
                {
                    Content = block.Code.ToList(),
                    ParentContainer = container
                };

                for (int i = 0; i < slotContent.Content.Count; i++)
                {
                    var line = slotContent.Content[i];
                    if (line.Contains("<Slot name="))
                    {
                        var nestedSlotName = line.Split('"')[1];
                        slotContent.NestedSlots[nestedSlotName] = null;
                    }
                }

                _slots[className][slotName] = slotContent;
            }
            else
            {
                if (!_classes[className].AdditionalData.ContainsKey("XAML"))
                {
                    _classes[className].AdditionalData["XAML"] = new List<string>();
                }
                _classes[className].AdditionalData["XAML"].AddRange(block.Code);
            }
        }

        private void ProcessCommand(string className, AnnotatedCode block)
        {
            var methodName = block.Annotations[0].Values.First(v => v.Key == "functionName").Value;
            var commandName = methodName + "Command";

            var currentClass = _classes[className];

            currentClass.Properties.Add(new TranspiledProperty
            {
                Name = commandName,
                Type = "ICommand"
            });

            currentClass.ConstructorLines.Add($"this.{commandName} = new RelayCommand({methodName});");

            var method = new TranspiledMethod
            {
                Name = methodName,
                Code = block.Code
            };
            currentClass.Methods.Add(method);

            if (!_commandMethods.ContainsKey(className))
            {
                _commandMethods[className] = new();
            }
            _commandMethods[className].Add(methodName);
        }

        protected override void ProcessEvent(TranspiledClass currentClass, AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var eventName = annotation.Values.First(v => v.Key == "event").Value;
            var target = annotation.Values.First(v => v.Key == "target").Value;

            var method = new TranspiledMethod
            {
                Name = $"{target}_{eventName}",
                Parameters = new() { "object sender", "RoutedEventArgs e" },
                Code = block.Code
            };
            currentClass.Methods.Add(method);
        }

        private List<string> FillSlots(string className, List<string> xaml, HashSet<string> filledSlots = null)
        {
            filledSlots ??= new HashSet<string>();
            var result = new List<string>();

            foreach (var line in xaml)
            {
                if (line.Contains("<Slot name="))
                {
                    var slotName = line.Split('"')[1];
                    if (!filledSlots.Contains(slotName) && _slots[className].ContainsKey(slotName))
                    {
                        filledSlots.Add(slotName);
                        var slotContent = _slots[className][slotName];
                        var filledContent = FillSlots(className, slotContent.Content, filledSlots);
                        result.AddRange(filledContent);
                    }
                }
                else
                {
                    result.Add(line);
                }
            }

            return result;
        }

        public override string GenerateOutput()
        {
            var sb = new StringBuilder();

            // Add imports
            foreach (var import in _imports)
            {
                sb.AppendLine(import);
            }
            sb.AppendLine();

            // Generate services
            foreach (var (serviceName, serviceCode) in _services)
            {
                var interfaceName = $"I{serviceName}";
                var signatures = ExtractMethodSignatures(serviceCode);

                // Interface
                sb.AppendLine($"public interface {interfaceName}");
                sb.AppendLine("{");
                foreach (var signature in signatures)
                {
                    sb.AppendLine($"    {signature};");
                }
                sb.AppendLine("}");
                sb.AppendLine();

                // Implementation
                sb.AppendLine($"public class {serviceName} : {interfaceName}");
                sb.AppendLine("{");
                foreach (var line in serviceCode)
                {
                    sb.AppendLine($"    {line}");
                }
                sb.AppendLine("}");
                sb.AppendLine();
            }

            // Generate ViewModelLocator
            GenerateViewModelLocator(sb);
            sb.AppendLine();

            GenerateRelayCommandClass(sb);
            sb.AppendLine();

            foreach (var classInfo in _classes.Values)
            {
                // Fill slots before generating XAML
                if (classInfo.AdditionalData.ContainsKey("XAML"))
                {
                    classInfo.AdditionalData["XAML"] =
                        FillSlots(classInfo.ClassName, classInfo.AdditionalData["XAML"]);
                }

                // Generate code-behind
                sb.AppendLine($"public partial class {classInfo.ClassName} : {classInfo.BaseClass}");
                sb.AppendLine("{");

                sb.AppendLine("\tpublic event PropertyChangedEventHandler PropertyChanged;");
                sb.AppendLine();

                GeneratePropertyChangedMethod(sb);
                sb.AppendLine();

                foreach (var prop in classInfo.Properties)
                {
                    GenerateProperty(sb, prop);
                }
                sb.AppendLine();

                sb.AppendLine($"\tpublic {classInfo.ClassName}()");
                sb.AppendLine("\t{");
                sb.AppendLine("\t\tInitializeComponent();");
                foreach (var line in classInfo.ConstructorLines)
                {
                    sb.AppendLine($"\t\t{line}");
                }
                sb.AppendLine("\t}");
                sb.AppendLine();

                foreach (var method in classInfo.Methods)
                {
                    GenerateMethod(sb, method);
                }

                sb.AppendLine("}");
                sb.AppendLine();

                // Generate XAML
                if (classInfo.AdditionalData.ContainsKey("XAML"))
                {
                    GenerateXaml(sb, classInfo);
                }
            }

            return sb.ToString();
        }

        private void GenerateRelayCommandClass(StringBuilder sb)
        {
            sb.AppendLine("public class RelayCommand : ICommand");
            sb.AppendLine("{");
            sb.AppendLine("\tprivate readonly Action _execute;");
            sb.AppendLine("\tprivate readonly Func<bool> _canExecute;");
            sb.AppendLine();
            sb.AppendLine("\tpublic RelayCommand(Action execute, Func<bool> canExecute = null)");
            sb.AppendLine("\t{");
            sb.AppendLine("\t\t_execute = execute ?? throw new ArgumentNullException(nameof(execute));");
            sb.AppendLine("\t\t_canExecute = canExecute;");
            sb.AppendLine("\t}");
            sb.AppendLine();
            sb.AppendLine("\tpublic event EventHandler CanExecuteChanged");
            sb.AppendLine("\t{");
            sb.AppendLine("\t\tadd { CommandManager.RequerySuggested += value; }");
            sb.AppendLine("\t\tremove { CommandManager.RequerySuggested -= value; }");
            sb.AppendLine("\t}");
            sb.AppendLine();
            sb.AppendLine("\tpublic bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;");
            sb.AppendLine();
            sb.AppendLine("\tpublic void Execute(object parameter) => _execute();");
            sb.AppendLine("}");
        }

        private void GeneratePropertyChangedMethod(StringBuilder sb)
        {
            sb.AppendLine("\tprotected void OnPropertyChanged([CallerMemberName] string propertyName = null)");
            sb.AppendLine("\t{");
            sb.AppendLine("\t\tPropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));");
            sb.AppendLine("\t}");
        }

        private void GenerateProperty(StringBuilder sb, TranspiledProperty prop)
        {
            var fieldName = $"_{char.ToLower(prop.Name[0])}{prop.Name.Substring(1)}";

            sb.AppendLine($"\tprivate {prop.Type} {fieldName};");
            sb.AppendLine($"\tpublic {prop.Type} {prop.Name}");
            sb.AppendLine("\t{");
            sb.AppendLine($"\t\tget => {fieldName};");
            sb.AppendLine("\t\tset");
            sb.AppendLine("\t\t{");
            sb.AppendLine($"\t\t\tif ({fieldName} != value)");
            sb.AppendLine("\t\t\t{");
            sb.AppendLine($"\t\t\t\t{fieldName} = value;");
            sb.AppendLine($"\t\t\t\tOnPropertyChanged();");
            sb.AppendLine("\t\t\t}");
            sb.AppendLine("\t\t}");
            sb.AppendLine("\t}");
        }

        private void GenerateMethod(StringBuilder sb, TranspiledMethod method)
        {
            var parameters = string.Join(", ", method.Parameters);
            sb.AppendLine($"\tprivate void {method.Name}({parameters})");
            sb.AppendLine("\t{");
            foreach (var line in method.Code)
            {
                sb.AppendLine($"\t\t{line}");
            }
            sb.AppendLine("\t}");
        }

        private void GenerateXaml(StringBuilder sb, TranspiledClass classInfo)
        {
            sb.AppendLine($"<Window x:Class=\"{classInfo.ClassName}\"");
            sb.AppendLine("        xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"");
            sb.AppendLine("        xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"");
            sb.AppendLine("        Title=\"Window\" Height=\"450\" Width=\"800\">");

            foreach (var xamlLine in classInfo.AdditionalData["XAML"])
            {
                sb.AppendLine($"    {xamlLine}");
            }

            sb.AppendLine("</Window>");
        }

        private void GenerateViewModelLocator(StringBuilder sb)
        {
            sb.AppendLine("public class ViewModelLocator");
            sb.AppendLine("{");
            sb.AppendLine("    private static IServiceProvider _serviceProvider;");
            sb.AppendLine();
            sb.AppendLine("    public ViewModelLocator()");
            sb.AppendLine("    {");
            sb.AppendLine("        var services = new ServiceCollection();");

            // Register services
            foreach (var serviceName in _services.Keys)
            {
                var lifetime = "Transient"; // Or get from annotation
                sb.AppendLine($"        services.Add{lifetime}<I{serviceName}, {serviceName}>();");
            }

            // Register ViewModels
            foreach (var className in _classes.Keys.Where(c => c.EndsWith("ViewModel")))
            {
                sb.AppendLine($"        services.AddTransient<{className}>();");
            }

            sb.AppendLine("        _serviceProvider = services.BuildServiceProvider();");
            sb.AppendLine("    }");

            // Add properties for each ViewModel
            foreach (var className in _classes.Keys.Where(c => c.EndsWith("ViewModel")))
            {
                var propName = className.Replace("ViewModel", "");
                sb.AppendLine();
                sb.AppendLine($"    public {className} {propName} =>");
                sb.AppendLine($"        _serviceProvider.GetRequiredService<{className}>();");
            }

            sb.AppendLine("}");
        }

        private List<string> ExtractMethodSignatures(List<string> serviceCode)
        {
            var signatures = new List<string>();
            foreach (var line in serviceCode)
            {
                if (line.Contains("public") && (line.Contains("async") || line.Contains("Task") || line.Contains("void") || line.Contains("bool") || line.Contains("string") || line.Contains("int")))
                {
                    // Remove implementation if it's on the same line
                    var signature = line.Split('{')[0].Trim();
                    // Remove 'public' keyword for interface
                    signature = signature.Replace("public ", "");
                    // Clean up any trailing whitespace or semicolons
                    signature = signature.TrimEnd(';', ' ');
                    signatures.Add(signature);
                }
            }
            return signatures;
        }
    }
}
