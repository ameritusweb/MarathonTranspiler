using MarathonTranspiler.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.Python
{
    public partial class PythonTranspiler : MarathonTranspilerBase
    {
        protected override void ProcessVarInit(TranspiledClass currentClass, AnnotatedCode block)
        {
            var type = block.Annotations[0].Values.First(v => v.Key == "type").Value;
            var code = block.Code[0];

            switch (type)
            {
                case "ndarray":
                    _imports.Add("import numpy as np");
                    if (code.Contains("linspace"))
                        _imports.Add("from numpy import linspace");
                    if (code.Contains("random"))
                        _imports.Add("from numpy import random");
                    break;

                case "Symbol":
                case "Expr":
                    _imports.Add("from sympy import Symbol, solve, expand, factor");
                    if (code.Contains("diff"))
                        _imports.Add("from sympy import diff");
                    if (code.Contains("integrate"))
                        _imports.Add("from sympy import integrate");
                    break;

                case "Matrix":
                    _imports.Add("from sympy import Matrix");
                    break;
            }

            if (!code.StartsWith("self."))
            {
                currentClass.Fields.Add(code);
            }
        }
    }
}
