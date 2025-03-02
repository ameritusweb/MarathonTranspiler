using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Xml.Linq;
using Acornima;
using Acornima.Ast;
using MarathonTranspiler.LSP.Model;
using Microsoft.CodeAnalysis.Text;

namespace MarathonTranspiler.LSP.Extensions
{
    public class ScriptParser
    {
        private string sourceText;

        public List<MethodInfo> ParseFile(string filePath)
        {
            var methods = new List<MethodInfo>();
            this.sourceText = File.ReadAllText(filePath);
            ParserOptions options = new ParserOptions
            {
                 EcmaVersion = EcmaVersion.ES2020,
                 AllowImportExportEverywhere = true,
                 AllowReturnOutsideFunction = true
            };
            var parser = new Parser(options);
            var program = parser.ParseScript(this.sourceText);

            VisitNode(program, methods, filePath);
            return methods;
        }

        private void VisitNode(Node node, List<MethodInfo> methods, string filePath)
        {
            switch (node)
            {
                case ClassDeclaration classDecl:
                    foreach (var member in classDecl.Body.Body)
                    {
                        if (member is MethodDefinition methodDef)
                        {
                            if (methodDef.Static)
                            {
                                var functionExpr = methodDef.Value.As<FunctionExpression>();
                                var parameterUsages = new List<ParameterUsage>();

                                // Create parameter usage trackers
                                foreach (var param in functionExpr.Params)
                                {
                                    if (param is Identifier identifier)
                                    {
                                        parameterUsages.Add(new ParameterUsage
                                        {
                                            Name = identifier.Name,
                                            Locations = new List<int>()
                                        });
                                    }
                                }

                                // Track parameter usages in the body
                                CollectParameterUsages(functionExpr.Body, parameterUsages);

                                var methodFullText = this.sourceText.Substring(
                                   methodDef.Start,
                                   methodDef.End - methodDef.Start
                               );

                                var method = new MethodInfo
                                {
                                    Name = methodDef.Key.As<Identifier>().Name,
                                    Body = ExtractBody(methodDef.Value.As<FunctionExpression>().Body),
                                    FullText = methodFullText,
                                    Parameters = ExtractParameters(methodDef.Value.As<FunctionExpression>()),
                                    ParameterUsages = parameterUsages,
                                    IsStatic = true,
                                    SourceFile = filePath,
                                    Dependencies = ExtractDependencies(methodDef),
                                    ClassName = classDecl.Id!.Name,
                                    BodyStartIndex = functionExpr.Body.Start
                                };
                                methods.Add(method);
                            }
                        }
                    }
                    break;
            }

            foreach (var child in node.ChildNodes)
            {
                VisitNode(child, methods, filePath);
            }
        }

        private void CollectParameterUsages(Node node, List<ParameterUsage> parameterUsages)
        {
            if (node is Identifier identifier)
            {
                var usage = parameterUsages.FirstOrDefault(p => p.Name == identifier.Name);
                if (usage != null)
                {
                    usage.Locations.Add(identifier.Start);
                }
            }

            foreach (var child in node.ChildNodes)
            {
                CollectParameterUsages(child, parameterUsages);
            }
        }

        private string ExtractBody(BlockStatement body)
        {
            // Extract the actual source text using location information
            var start = body.Start;
            var end = body.End;
            return sourceText.Substring(start, end - start);
        }

        private List<string> ExtractParameters(IFunction function)
        {
            var parameters = new List<string>();
            foreach (var param in function.Params)
            {
                if (param is Identifier identifier)
                {
                    parameters.Add(identifier.Name);
                }
            }
            return parameters;
        }

        private List<string> ExtractDependencies(MethodDefinition methodDef)
        {
            var dependencies = new List<string>();

            // Look for JSDoc comments with @dependency tag
            if (methodDef.Decorators.Any())
            {
                foreach (var comment in methodDef.Decorators)
                {
                    if (comment != null && comment.ToString()!.Contains("@dependency"))
                    {
                        // Extract dependency values from comment
                        var matches = Regex.Matches(comment.ToString()!, @"@dependency\s+(.+?)(?=\s*@|\s*\*\/|$)");
                        foreach (Match match in matches)
                        {
                            if (match.Groups.Count > 1)
                            {
                                dependencies.Add(match.Groups[1].Value.Trim());
                            }
                        }
                    }
                }
            }

            return dependencies;
        }
    }
}
