using MarathonTranspiler.Core;
using MarathonTranspiler.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.Unity
{
    public class UnityTranspiler : MarathonTranspilerBase
    {
        private readonly HashSet<string> _imports = new()
        {
            "using UnityEngine;",
            "using UnityEngine.Events;",
            "using System.Collections;",
        };
        private readonly UnityConfig _config;

        public UnityTranspiler(UnityConfig config)
        {
            this._config = config;
        }

        protected internal override void ProcessBlock(AnnotatedCode block, AnnotatedCode previousBlock)
        {
            var mainAnnotation = block.Annotations[0];
            var className = mainAnnotation.Values.First(v => v.Key == "className").Value;

            if (!_classes.ContainsKey(className))
            {
                _classes[className] = new TranspiledClass
                {
                    ClassName = className,
                    BaseClass = "MonoBehaviour"
                };
            }

            var currentClass = _classes[className];

            switch (mainAnnotation.Name)
            {
                case "varInit":
                    var type = mainAnnotation.Values.First(v => v.Key == "type").Value;
                    if (type.Contains("Component") || type == "Rigidbody" || type == "Transform")
                    {
                        currentClass.Fields.Add("[SerializeField] " + block.Code[0]);
                    }
                    else
                    {
                        base.ProcessBlock(block, previousBlock);
                    }
                    break;

                case "start":
                case "update":
                case "fixedUpdate":
                case "lateUpdate":
                case "awake":
                    var method = new TranspiledMethod
                    {
                        Name = char.ToUpper(mainAnnotation.Name[0]) + mainAnnotation.Name.Substring(1),
                        Code = block.Code
                    };
                    currentClass.Methods.Add(method);
                    break;

                case "onCollisionEnter":
                case "onTriggerEnter":
                case "onMouseDown":
                    var handler = new TranspiledMethod
                    {
                        Name = char.ToUpper(mainAnnotation.Name[0]) + mainAnnotation.Name.Substring(1),
                        Parameters = block.Annotations.Skip(1)
                            .Where(a => a.Name == "parameter")
                            .Select(a => $"{a.Values.First(v => v.Key == "type").Value} {a.Values.First(v => v.Key == "name").Value}")
                            .ToList(),
                        Code = block.Code
                    };
                    currentClass.Methods.Add(handler);
                    break;

                case "coroutine":
                    var coroutineName = mainAnnotation.Values.First(v => v.Key == "functionName").Value;
                    var coroutine = new TranspiledMethod
                    {
                        Name = coroutineName,
                        IsCoroutine = true,
                        Code = block.Code
                    };
                    currentClass.Methods.Add(coroutine);
                    break;

                case "unityEvent":
                    var eventName = mainAnnotation.Values.First(v => v.Key == "name").Value;
                    var eventType = mainAnnotation.Values.FirstOrDefault(v => v.Key == "type").Value ?? "UnityEvent";
                    currentClass.Fields.Add($"public {eventType} {eventName} = new {eventType}();");
                    break;

                default:
                    base.ProcessBlock(block, previousBlock);
                    break;
            }
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

            foreach (var classInfo in _classes.Values)
            {
                // Unity component class
                sb.AppendLine($"public class {classInfo.ClassName} : {classInfo.BaseClass}");
                sb.AppendLine("{");

                // Fields
                foreach (var field in classInfo.Fields)
                {
                    sb.AppendLine($"    {field}");
                }
                if (classInfo.Fields.Any()) sb.AppendLine();

                // Methods
                foreach (var method in classInfo.Methods)
                {
                    if (method.IsCoroutine)
                    {
                        sb.AppendLine($"    private IEnumerator {method.Name}()");
                    }
                    else
                    {
                        var parameters = string.Join(", ", method.Parameters);
                        sb.AppendLine($"    private void {method.Name}({parameters})");
                    }
                    sb.AppendLine("    {");
                    foreach (var line in method.Code)
                    {
                        sb.AppendLine($"        {line}");
                    }
                    sb.AppendLine("    }");
                    sb.AppendLine();
                }

                sb.AppendLine("}");
            }

            return sb.ToString();
        }
    }
}
