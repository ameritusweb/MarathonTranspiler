using MarathonTranspiler.Core;
using MarathonTranspiler.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.CSharp
{
    public partial class CSharpTranspiler : MarathonTranspilerBase
    {
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
    }
}
