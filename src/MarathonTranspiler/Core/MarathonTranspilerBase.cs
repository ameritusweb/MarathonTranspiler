using MarathonTranspiler.Extensions;
using MarathonTranspiler.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Core
{
    public abstract class MarathonTranspilerBase
    {
        protected readonly Dictionary<string, TranspiledClass> _classes = new();
        protected readonly List<string> _mainMethodLines = new();
        protected readonly Dictionary<string, string> _idToClassNameMap = new();
        protected Dictionary<string, TranspiledComponent> Components { get; } = new();
        protected Dictionary<string, TranspiledPage> Pages { get; } = new();

        public void ProcessAnnotatedCode(List<AnnotatedCode> annotatedCodes)
        {
            AnnotatedCode? previousBlock = null;
            foreach (var block in annotatedCodes)
            {
                ProcessBlock(block, previousBlock);
                previousBlock = block;
            }
        }

        protected TranspiledPage GetOrCreatePage(string route)
        {
            if (!Pages.ContainsKey(route))
            {
                Pages[route] = new TranspiledPage { Route = route };
            }
            return Pages[route];
        }

        protected TranspiledComponent GetOrCreateComponent(string name)
        {
            if (!Components.ContainsKey(name))
            {
                Components[name] = new TranspiledComponent { Name = name };
            }
            return Components[name];
        }

        protected internal virtual void ProcessBlock(AnnotatedCode block, AnnotatedCode? previousBlock)
        {
            var mainAnnotation = block.Annotations[0];
            var className = mainAnnotation.Values.GetValue("className", string.Empty);

            if (className == string.Empty && mainAnnotation.Name == "more")
            {
                var cid = mainAnnotation.Values.GetValue("id", string.Empty);
                className = _idToClassNameMap[cid];
            }

            foreach (var a in block.Annotations)
            {
                var id = a.Values.GetValue("id", string.Empty);
                if (id != string.Empty && className != string.Empty && !_idToClassNameMap.ContainsKey(id))
                {
                    _idToClassNameMap.Add(id, className);
                }
            }

            if (string.IsNullOrWhiteSpace(className))
            {
                throw new Exception("Class name not found.");
            }

            if (!_classes.ContainsKey(className))
            {
                _classes[className] = new TranspiledClass { ClassName = className };
            }

            var currentClass = _classes[className];

            switch (mainAnnotation.Name)
            {
                case "varInit":
                    ProcessVarInit(currentClass, block);
                    break;

                case "run":
                    ProcessRun(currentClass, block);
                    break;

                case "assert":
                    ProcessAssert(currentClass, block);
                    break;

                case "onEvent":
                    ProcessEvent(currentClass, block);
                    break;

                case "inject":
                    ProcessInject(currentClass, block);
                    break;

                case "more":
                    ProcessMore(currentClass, block);
                    break;

                case "hook":
                    ProcessHook(currentClass, block);
                    break;

                case "xml":
                    if (mainAnnotation.Values.Any(v => v.Key == "pageName"))
                    {
                        var pageName = mainAnnotation.Values.First(v => v.Key == "pageName").Value;
                        var currentPage = GetOrCreatePage(pageName);
                        ProcessXml(currentPage, block);
                    }
                    else if (mainAnnotation.Values.Any(v => v.Key == "componentName"))
                    {
                        var componentName = mainAnnotation.Values.First(v => v.Key == "componentName").Value;
                        var currentComponent = GetOrCreateComponent(componentName);
                        ProcessXml(currentComponent, block);
                    }
                    break;
            }
        }

        protected virtual void ProcessInject(TranspiledClass currentClass, AnnotatedCode block) { }

        protected virtual void ProcessHook(TranspiledClass currentClass, AnnotatedCode block) { }

        protected virtual void ProcessVarInit(TranspiledClass currentClass, AnnotatedCode block)
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

        protected virtual void ProcessRun(TranspiledClass currentClass, AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var functionName = annotation.Values.First(v => v.Key == "functionName").Value;
            var method = GetOrCreateMethod(currentClass, functionName);

            foreach (var paramAnnotation in block.Annotations.Skip(1))
            {
                if (paramAnnotation.Name == "parameter")
                {
                    var param = $"{paramAnnotation.Values.First(v => v.Key == "type").Value} {paramAnnotation.Values.First(v => v.Key == "name").Value}";
                    if (!method.Parameters.Contains(param))
                    {
                        method.Parameters.Add(param);
                    }
                }
            }

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

        protected virtual void ProcessAssert(TranspiledClass currentClass, AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var condition = annotation.Values.First(v => v.Key == "condition").Value;
            var message = block.Code[0].Trim('"');

            // Handle "after" attribute for assertions that should run after a specific method
            if (annotation.Values.Any(v => v.Key == "after"))
            {
                var afterMethod = annotation.Values.First(v => v.Key == "after").Value;
                var method = GetOrCreateMethod(currentClass, afterMethod);
                method.Code.Add($"Assert.True({condition}, \"{message}\");");
            }
            else
            {
                currentClass.Assertions.Add($"Assert.True({condition}, \"{message}\");");
            }
        }

        protected virtual void ProcessMore(TranspiledClass currentClass, AnnotatedCode block)
        {
            // Platform-specific event handling to be implemented by derived classes
        }

        protected virtual void ProcessEvent(TranspiledClass currentClass, AnnotatedCode block)
        {
            // Platform-specific event handling to be implemented by derived classes
        }

        protected virtual void ProcessXml(TranspiledPage currentPage, AnnotatedCode block)
        {
            // Platform-specific XML processing to be implemented by derived classes
        }

        protected virtual void ProcessXml(TranspiledComponent currentComponent, AnnotatedCode block)
        {
            // Platform-specific XML processing to be implemented by derived classes
        }

        protected TranspiledMethod GetOrCreateMethod(TranspiledClass currentClass, string methodName)
        {
            var method = currentClass.Methods.FirstOrDefault(m => m.Name == methodName);
            if (method == null)
            {
                method = new TranspiledMethod { Name = methodName };
                currentClass.Methods.Add(method);
            }
            return method;
        }

        public abstract string GenerateOutput();
    }
}
