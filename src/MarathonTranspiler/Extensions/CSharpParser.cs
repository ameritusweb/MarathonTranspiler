using MarathonTranspiler.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MarathonTranspiler.Extensions
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
                    var methodInfo = new MethodInfo
                    {
                        Name = method.Identifier.Text,
                        Body = ExtractBody(method),
                        Parameters = ExtractParameters(method),
                        IsStatic = true,
                        SourceFile = filePath,
                        Dependencies = ExtractDependencies(method)
                    };
                    methods.Add(methodInfo);
                }
            }
            return methods;
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
