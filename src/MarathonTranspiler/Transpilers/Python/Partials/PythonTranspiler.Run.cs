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
        protected override void ProcessRun(TranspiledClass currentClass, AnnotatedCode block)
        {
            var code = string.Join("\n", block.Code);

            // Detect numpy operations
            if (code.Contains("dot") || code.Contains("matmul"))
                _imports.Add("from numpy import dot, matmul");
            if (code.Contains("eigenvals") || code.Contains("eigenvects"))
                _imports.Add("from numpy.linalg import eig");
            if (code.Contains("inv"))
                _imports.Add("from numpy.linalg import inv");

            // Detect sympy operations
            if (code.Contains("limit"))
                _imports.Add("from sympy import limit");
            if (code.Contains("series"))
                _imports.Add("from sympy import series");
            if (code.Contains("simplify"))
                _imports.Add("from sympy import simplify");

            var methodName = block.Annotations[0].Values.First(v => v.Key == "functionName").Value;
            var method = new TranspiledMethod { Name = methodName };

            foreach (var annotation in block.Annotations.Skip(1))
            {
                if (annotation.Name == "parameter")
                {
                    var paramType = annotation.Values.First(v => v.Key == "type").Value;
                    var paramName = annotation.Values.First(v => v.Key == "name").Value;
                    method.Parameters.Add($"{paramName}: {paramType}");
                }
            }

            if (block.Annotations[0].Values.Any(v => v.Key == "returnType"))
            {
                method.ReturnType = block.Annotations[0].Values.First(v => v.Key == "returnType").Value;
            }

            method.Code = block.Code;
            currentClass.Methods.Add(method);
        }
    }
}
