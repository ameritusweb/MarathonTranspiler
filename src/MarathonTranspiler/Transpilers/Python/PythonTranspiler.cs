using MarathonTranspiler.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.Python
{
    public class PythonTranspiler : MarathonTranspilerBase
    {
        private readonly HashSet<string> _imports = new();
        private PythonConfig _config;

        public PythonTranspiler(PythonConfig config)
        {
            this._config = config;
            _imports.Add("from abc import ABC, abstractmethod");
            _imports.Add("from typing import List, Dict, Optional");
        }

        protected override void ProcessBlock(AnnotatedCode block)
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

        public override string GenerateOutput()
        {
            var sb = new StringBuilder();

            // Imports
            foreach (var import in _imports.OrderBy(i => i))
            {
                sb.AppendLine(import);
            }
            sb.AppendLine();

            // Classes
            foreach (var classInfo in _classes.Values)
            {
                // Class definition with optional ABC inheritance
                var baseClass = classInfo.IsAbstract ? "(ABC)" : "";
                sb.AppendLine($"class {classInfo.ClassName}{baseClass}:");

                // Class variables/fields
                foreach (var field in classInfo.Fields)
                {
                    sb.AppendLine($"    {field}");
                }
                if (classInfo.Fields.Any()) sb.AppendLine();

                // Methods
                foreach (var method in classInfo.Methods)
                {
                    // Method decorators
                    if (method.IsAbstract)
                        sb.AppendLine("    @abstractmethod");
                    if (method.IsStatic)
                        sb.AppendLine("    @staticmethod");
                    if (method.IsProperty)
                        sb.AppendLine("    @property");

                    // Method signature
                    var parameters = string.Join(", ", method.Parameters);
                    if (!method.IsStatic && !string.IsNullOrEmpty(parameters))
                        parameters = ", " + parameters;

                    var selfParam = method.IsStatic ? "" : "self";
                    if (!string.IsNullOrEmpty(parameters))
                        selfParam = selfParam + parameters;

                    var returnType = string.IsNullOrEmpty(method.ReturnType) ? "" : $" -> {method.ReturnType}";
                    sb.AppendLine($"    def {method.Name}({selfParam}){returnType}:");

                    // Method body
                    if (method.Code.Any())
                    {
                        foreach (var line in method.Code)
                        {
                            sb.AppendLine($"        {line}");
                        }
                    }
                    else
                    {
                        sb.AppendLine("        pass");
                    }
                    sb.AppendLine();
                }
            }

            // Main execution
            sb.AppendLine("if __name__ == '__main__':");
            foreach (var className in _classes.Keys.Where(c => !_classes[c].IsAbstract))
            {
                var instanceName = char.ToLower(className[0]) + className.Substring(1);
                sb.AppendLine($"    {instanceName} = {className}()");
            }
            foreach (var line in _mainMethodLines)
            {
                sb.AppendLine($"    {line}");
            }

            return sb.ToString();
        }
    }
}
