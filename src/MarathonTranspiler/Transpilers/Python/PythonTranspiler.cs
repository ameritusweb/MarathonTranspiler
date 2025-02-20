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
        private readonly HashSet<string> _imports = new();
        private PythonConfig _config;

        public PythonTranspiler(PythonConfig config)
        {
            this._config = config;
            _imports.Add("from abc import ABC, abstractmethod");
            _imports.Add("from typing import List, Dict, Optional");
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
