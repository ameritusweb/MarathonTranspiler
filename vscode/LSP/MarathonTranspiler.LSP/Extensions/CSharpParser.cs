using MarathonTranspiler.LSP.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.LSP.Extensions
{
    public class CSharpParser
    {
        public List<MethodInfo> ParseFile(string filePath)
        {
            var methods = new List<MethodInfo>();
            var sourceCode = File.ReadAllText(filePath);

            // Create syntax tree from the source code
            SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetRoot();
            // Find all method declarations
            var methodDeclarations = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>();
            foreach (var method in methodDeclarations)
            {
                if (IsStaticMethod(method))
                {
                    var className = method.Ancestors()
                        .OfType<ClassDeclarationSyntax>()
                        .FirstOrDefault()?.Identifier.Text;

                    var parameterUsages = CollectParameterUsages(method);

                    var methodWithTrivia = method.GetLeadingTrivia().ToString() +
                                         method.ToString();

                    var methodInfo = new MethodInfo
                    {
                        Name = method.Identifier.Text,
                        Body = ExtractBody(method),
                        FullText = methodWithTrivia,
                        Parameters = ExtractParameters(method),
                        ParameterUsages = parameterUsages,
                        IsStatic = true,
                        SourceFile = filePath,
                        Dependencies = ExtractDependencies(method),
                        ClassName = className!,
                        BodyStartIndex = method.Body?.SpanStart ?? method.ExpressionBody?.SpanStart ?? 0
                    };
                    methods.Add(methodInfo);
                }
            }
            return methods;
        }

        private List<ParameterUsage> CollectParameterUsages(MethodDeclarationSyntax method)
        {
            var parameterUsages = method.ParameterList.Parameters
                .Select(p => new ParameterUsage
                {
                    Name = p.Identifier.Text,
                    Locations = new List<int>()
                })
                .ToList();

            // For regular method body
            if (method.Body != null)
            {
                CollectIdentifierLocations(method.Body, parameterUsages);
            }
            // For expression-bodied members
            else if (method.ExpressionBody != null)
            {
                CollectIdentifierLocations(method.ExpressionBody.Expression, parameterUsages);
            }

            return parameterUsages;
        }

        private void CollectIdentifierLocations(SyntaxNode node, List<ParameterUsage> parameterUsages)
        {
            if (node is IdentifierNameSyntax identifier)
            {
                var usage = parameterUsages.FirstOrDefault(p => p.Name == identifier.Identifier.Text);
                if (usage != null)
                {
                    usage.Locations.Add(identifier.SpanStart);
                }
            }

            foreach (var child in node.ChildNodes())
            {
                CollectIdentifierLocations(child, parameterUsages);
            }
        }

        private bool IsStaticMethod(MethodDeclarationSyntax method)
        {
            return method.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
        }
        private string ExtractBody(MethodDeclarationSyntax method)
        {
            if (method.Body != null)
            {
                // Get the body text including all whitespace and comments
                return method.Body.ToFullString();
            }
            else if (method.ExpressionBody != null)
            {
                // Handle expression-bodied members
                return $"=> {method.ExpressionBody.Expression.ToFullString()}";
            }
            return string.Empty;
        }
        private List<string> ExtractParameters(MethodDeclarationSyntax method)
        {
            return method.ParameterList.Parameters
                .Select(p => p.Identifier.Text)
                .ToList();
        }

        private List<string> ExtractDependencies(MethodDeclarationSyntax method)
        {
            var dependencies = new List<string>();

            // Look for [Dependency("...")] attributes
            foreach (var attributeList in method.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    if (attribute.Name.ToString() == "Dependency" ||
                        attribute.Name.ToString() == "DependencyAttribute")
                    {
                        // Extract the argument value
                        if (attribute.ArgumentList?.Arguments.Count > 0)
                        {
                            var firstArg = attribute.ArgumentList.Arguments[0];
                            if (firstArg.Expression is LiteralExpressionSyntax literal &&
                                literal.Kind() == SyntaxKind.StringLiteralExpression)
                            {
                                string dependencyValue = literal.Token.ValueText;
                                dependencies.Add(dependencyValue);
                            }
                        }
                    }
                }
            }

            return dependencies;
        }
    }
}
