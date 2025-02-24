using MarathonTranspiler.Core;
using MarathonTranspiler.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.Python
{
    public partial class PythonTranspiler : MarathonTranspilerBase
    {
        protected internal override void ProcessBlock(AnnotatedCode block, AnnotatedCode previousBlock)
        {
            var mainAnnotation = block.Annotations[0];
            var className = mainAnnotation.Values.First(v => v.Key == "className").Value;

            if (!_classes.ContainsKey(className))
            {
                _classes[className] = new TranspiledClass
                {
                    ClassName = className,
                    IsAbstract = mainAnnotation.Values.Any(v => v.Key == "abstract" && v.Value == "true")
                };
            }

            var currentClass = _classes[className];

            switch (mainAnnotation.Name)
            {
                case "varInit":
                    ProcessVarInit(currentClass, block);
                    break;

                case "classVar":
                    var varType = mainAnnotation.Values.First(v => v.Key == "type").Value;
                    currentClass.Fields.Add(block.Code[0]);
                    break;

                case "staticmethod":
                    var staticMethodName = mainAnnotation.Values.First(v => v.Key == "functionName").Value;
                    var staticMethod = new TranspiledMethod
                    {
                        Name = staticMethodName,
                        IsStatic = true,
                        Code = block.Code
                    };
                    currentClass.Methods.Add(staticMethod);
                    break;

                case "property":
                    var propName = mainAnnotation.Values.First(v => v.Key == "functionName").Value;
                    var property = new TranspiledMethod
                    {
                        Name = propName,
                        IsProperty = true,
                        Code = block.Code
                    };
                    currentClass.Methods.Add(property);
                    break;

                case "abstractmethod":
                    var abstractMethodName = mainAnnotation.Values.First(v => v.Key == "functionName").Value;
                    var abstractMethod = new TranspiledMethod
                    {
                        Name = abstractMethodName,
                        IsAbstract = true,
                        Code = block.Code
                    };
                    if (mainAnnotation.Values.Any(v => v.Key == "returnType"))
                    {
                        abstractMethod.ReturnType = mainAnnotation.Values.First(v => v.Key == "returnType").Value;
                    }
                    currentClass.Methods.Add(abstractMethod);
                    break;

                case "run":
                    ProcessRun(currentClass, block);
                    break;
            }
        }
    }
}
