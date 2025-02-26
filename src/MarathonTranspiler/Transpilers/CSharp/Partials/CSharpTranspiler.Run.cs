using MarathonTranspiler.Core;
using MarathonTranspiler.Extensions;
using MarathonTranspiler.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
                else if (paramAnnotation.Name == "varInit")
                {
                    // Process inline class definition (varInit with type array)
                    ProcessNestedVarInit(paramAnnotation, method);
                }
            }

            method.Id = annotation.Values.GetValue("id");

            // Process the code for special control flow syntax before adding it to the method
            var processedCode = ProcessControlFlowSyntax(block.Code, functionName, currentClass);
            method.Code.AddRange(processedCode);

            if (!annotation.Values.Any(v => v.Key == "enumerableStart" || v.Key == "enumerableEnd"))
            {
                var paramValues = block.Annotations.Skip(1)
                    .Where(a => a.Name == "parameter")
                    .Select(a => a.Values.First(v => v.Key == "value").Value);

                var instanceName = char.ToLower(currentClass.ClassName[0]) + currentClass.ClassName.Substring(1);
                _mainMethodLines.Add($"{instanceName}.{functionName}({string.Join(", ", paramValues)});");
            }
        }

        private void ProcessNestedVarInit(Annotation annotation, TranspiledMethod method)
        {
            var className = annotation.Values.First(v => v.Key == "className").Value;

            // Check if the type contains an array of properties
            var typeValue = annotation.Values.First(v => v.Key == "type").Value;
            if (typeValue.StartsWith("[") && typeValue.EndsWith("]"))
            {
                // Create or get the class
                if (!_classes.ContainsKey(className))
                {
                    _classes[className] = new TranspiledClass { ClassName = className };
                }

                var targetClass = _classes[className];

                try
                {
                    // Parse the JSON array of properties
                    var properties = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(typeValue);

                    if (properties != null)
                    {
                        foreach (var property in properties)
                        {
                            if (property.TryGetValue("name", out var propName) &&
                                property.TryGetValue("type", out var propType))
                            {
                                targetClass.Properties.Add(new TranspiledProperty
                                {
                                    Name = propName,
                                    Type = propType
                                });
                            }
                        }
                    }
                }
                catch (JsonException ex)
                {
                    // Add a comment to the method about the parsing failure
                    method.Code.Add($"// Failed to parse properties for {className}: {ex.Message}");

                    // Manual parsing as fallback (simplified)
                    var propertiesPattern = @"\{\s*name\s*=\s*""([^""]+)""\s*,\s*type\s*=\s*""([^""]+)""\s*\}";
                    var matches = Regex.Matches(typeValue, propertiesPattern);

                    foreach (Match match in matches)
                    {
                        if (match.Groups.Count >= 3)
                        {
                            var propName = match.Groups[1].Value;
                            var propType = match.Groups[2].Value;

                            targetClass.Properties.Add(new TranspiledProperty
                            {
                                Name = propName,
                                Type = propType
                            });
                        }
                    }
                }
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
