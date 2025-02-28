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

namespace MarathonTranspiler.Transpilers.React
{
    public partial class ReactTranspiler : MarathonTranspilerBase
    {
        protected override void ProcessRun(TranspiledClass currentClass, AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var functionName = annotation.Values.First(v => v.Key == "functionName").Value;

            // Store the method ID if present
            var methodId = annotation.Values.GetValue("id");

            // Process the code for control flow syntax before adding it
            var processedCode = ProcessControlFlowSyntax(block.Code);

            // Check for nested class definition
            foreach (var paramAnnotation in block.Annotations.Skip(1))
            {
                if (paramAnnotation.Name == "varInit")
                {
                    // Process inline class definition
                    ProcessNestedVarInit(paramAnnotation);
                }
            }

            // Add the function to the output
            _mainMethodLines.Add($"const {functionName} = () => {{");
            _mainMethodLines.AddRange(processedCode.Select(line => $"    {line}"));
            _mainMethodLines.Add("};");
        }

        private void ProcessNestedVarInit(Annotation annotation)
        {
            var className = annotation.Values.First(v => v.Key == "className").Value;

            // Check if the type contains an array of properties
            var typeValue = annotation.Values.First(v => v.Key == "type").Value;
            if (typeValue.StartsWith("[") && typeValue.EndsWith("]"))
            {
                // For React, we'll generate a TypeScript interface or PropTypes
                _imports.Add("import PropTypes from 'prop-types';");

                try
                {
                    // Parse the JSON array of properties
                    var properties = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(typeValue);

                    if (properties != null)
                    {
                        var sb = new StringBuilder();

                        // Generate TypeScript interface if using TypeScript
                        if (_config.UseTypeScript)
                        {
                            sb.AppendLine($"interface {className}Props {{");
                            foreach (var property in properties)
                            {
                                if (property.TryGetValue("name", out var propName) &&
                                    property.TryGetValue("type", out var propType))
                                {
                                    // Convert C# types to TypeScript types
                                    var tsType = ConvertToTypeScriptType(propType);
                                    sb.AppendLine($"  {propName}: {tsType};");
                                }
                            }
                            sb.AppendLine("}");
                        }
                        else
                        {
                            // Generate PropTypes for JavaScript
                            sb.AppendLine($"{className}.propTypes = {{");
                            foreach (var property in properties)
                            {
                                if (property.TryGetValue("name", out var propName) &&
                                    property.TryGetValue("type", out var propType))
                                {
                                    // Convert C# types to PropTypes
                                    var propTypeValue = ConvertToPropType(propType);
                                    sb.AppendLine($"  {propName}: {propTypeValue},");
                                }
                            }
                            sb.AppendLine("};");
                        }

                        // Add the generated code to component definitions
                        _mainMethodLines.Add("// Generated component properties");
                        _mainMethodLines.AddRange(sb.ToString().Split('\n'));
                    }
                }
                catch (JsonException ex)
                {
                    // Add a comment about the parsing failure
                    _mainMethodLines.Add($"// Failed to parse properties for {className}: {ex.Message}");

                    // Manual parsing as fallback (simplified)
                    var propertiesPattern = @"\{\s*name\s*=\s*""([^""]+)""\s*,\s*type\s*=\s*""([^""]+)""\s*\}";
                    var matches = Regex.Matches(typeValue, propertiesPattern);

                    if (_config.UseTypeScript)
                    {
                        _mainMethodLines.Add($"interface {className}Props {{");
                        foreach (Match match in matches)
                        {
                            if (match.Groups.Count >= 3)
                            {
                                var propName = match.Groups[1].Value;
                                var propType = match.Groups[2].Value;
                                var tsType = ConvertToTypeScriptType(propType);
                                _mainMethodLines.Add($"  {propName}: {tsType};");
                            }
                        }
                        _mainMethodLines.Add("}");
                    }
                    else
                    {
                        _mainMethodLines.Add($"{className}.propTypes = {{");
                        foreach (Match match in matches)
                        {
                            if (match.Groups.Count >= 3)
                            {
                                var propName = match.Groups[1].Value;
                                var propType = match.Groups[2].Value;
                                var propTypeValue = ConvertToPropType(propType);
                                _mainMethodLines.Add($"  {propName}: {propTypeValue},");
                            }
                        }
                        _mainMethodLines.Add("};");
                    }
                }
            }
        }

        private string ConvertToTypeScriptType(string csharpType)
        {
            return csharpType.ToLower() switch
            {
                "string" => "string",
                "int" => "number",
                "long" => "number",
                "double" => "number",
                "float" => "number",
                "decimal" => "number",
                "bool" => "boolean",
                "datetime" => "Date",
                "object" => "any",
                _ => "any"
            };
        }

        private string ConvertToPropType(string csharpType)
        {
            return csharpType.ToLower() switch
            {
                "string" => "PropTypes.string",
                "int" => "PropTypes.number",
                "long" => "PropTypes.number",
                "double" => "PropTypes.number",
                "float" => "PropTypes.number",
                "decimal" => "PropTypes.number",
                "bool" => "PropTypes.bool",
                "datetime" => "PropTypes.instanceOf(Date)",
                "object" => "PropTypes.object",
                _ => "PropTypes.any"
            };
        }
    }
}
