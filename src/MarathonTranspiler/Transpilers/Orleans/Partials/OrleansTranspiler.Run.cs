using MarathonTranspiler.Core;
using MarathonTranspiler.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.Orleans
{
    public partial class OrleansTranspiler : MarathonTranspilerBase
    {
        protected override void ProcessRun(TranspiledClass currentClass, AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var functionName = annotation.Values.First(v => v.Key == "functionName").Value;
            var isAutomatic = annotation.Values.First(v => v.Key == "isAutomatic").Value == "true";
            var method = GetOrCreateMethod(currentClass, functionName);

            // Set method properties
            method.Id = annotation.Values.GetValue("id", string.Empty);
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
    }
}
