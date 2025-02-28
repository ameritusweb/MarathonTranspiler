using MarathonTranspiler.Model;
using MarathonTranspiler.Readers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MarathonTranspiler.Extensions
{
    public class StaticMethodInliner
    {
        private readonly StaticMethodRegistry _registry;
        private readonly HashSet<string> _addedDependencies = new HashSet<string>();

        public StaticMethodInliner(StaticMethodRegistry registry)
        {
            _registry = registry;
        }

        public string ProcessInlining(string code, out List<string> dependencies)
        {
            _addedDependencies.Clear();
            var reader = new MarathonReader();
            var inlineCalls = reader.ExtractInlineMethodCalls(code);

            foreach (var call in inlineCalls.OrderByDescending(c => c.StartIndex))
            {
                if (_registry.TryGetMethod(call.ClassName, call.MethodName, out var method))
                {
                    // Add dependencies
                    foreach (var dependency in method.Dependencies)
                    {
                        _addedDependencies.Add(dependency);
                    }

                    var inlinedCode = TransformMethodBody(method, call.Arguments);
                    code = code.Substring(0, call.StartIndex) +
                           inlinedCode +
                           code.Substring(call.StartIndex + call.Length);
                }
            }

            dependencies = _addedDependencies.ToList();
            return code;
        }

        private string TransformMethodBody(MethodInfo method, List<string> arguments)
        {
            string body = method.Body;

            // Strip outer braces and adjust indentation
            body = RemoveOuterBraces(body);

            // Replace parameters with arguments
            for (int i = 0; i < Math.Min(method.Parameters.Count, arguments.Count); i++)
            {
                var parameter = method.Parameters[i];
                var argument = arguments[i];

                // Use regex to replace only parameter references, not variable declarations
                body = Regex.Replace(body, $@"(?<!\w){parameter}(?!\w)", argument);
            }

            return body;
        }

        private string RemoveOuterBraces(string body)
        {
            // Simple implementation - might need to be more robust
            body = body.Trim();
            if (body.StartsWith("{") && body.EndsWith("}"))
            {
                body = body.Substring(1, body.Length - 2).Trim();
            }

            return body;
        }
    }
}
