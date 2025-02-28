using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Xml.Linq;
using MarathonTranspiler.Model;
using Acornima.Ast;
using Acornima;

namespace MarathonTranspiler.Readers
{
    public class ScriptParser
    {
        public List<MethodInfo> ParseFile(string filePath)
        {
            var methods = new List<MethodInfo>();
            var source = File.ReadAllText(filePath);
            ParserOptions options = ParserOptions.Default;
            var parser = new Parser(options);
            var program = parser.ParseScript(source);

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
                        if (member is Acornima.Ast.MethodDefinition methodDef)
                        {
                            if (methodDef.Static)
                            {
                                var method = new MethodInfo
                                {
                                    Name = methodDef.Key.As<Identifier>().Name,
                                    Body = ExtractBody(methodDef.Value.As<FunctionExpression>().Body),
                                    Parameters = ExtractParameters(methodDef.Value.As<FunctionExpression>()),
                                    IsStatic = true,
                                    SourceFile = filePath
                                };
                                methods.Add(method);
                            }
                        }
                    }
                    break;

                case FunctionDeclaration funcDecl:
                    var functionMethod = new MethodInfo
                    {
                        Name = funcDecl.Id.Name,
                        Body = ExtractBody(funcDecl.Body),
                        Parameters = ExtractParameters(funcDecl),
                        IsStatic = true, // Consider all standalone functions as static
                        SourceFile = filePath
                    };
                    methods.Add(functionMethod);
                    break;
            }

            foreach (var child in node.ChildNodes)
            {
                VisitNode(child, methods, filePath);
            }
        }

        private string ExtractBody(BlockStatement body)
        {
            // This is a simplified implementation - you might want to use a proper source map
            // or source generation tool for more accurate body extraction
            return body.ToString();
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
    }
}
