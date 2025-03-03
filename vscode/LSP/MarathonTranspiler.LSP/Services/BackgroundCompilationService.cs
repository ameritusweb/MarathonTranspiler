using MarathonTranspiler.LSP.Model;
using MarathonTranspiler.Transpilers.CSharp;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using MarathonTranspiler.Model;

namespace MarathonTranspiler.LSP.Services
{
    public class BackgroundCompilationService
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly CSharpConfig _config;

        public BackgroundCompilationService(CSharpConfig config)
        {
            _config = config;
        }

        public async Task<List<CompilationError>> CompileAsync(string generatedCode)
        {
            _generatedCode = generatedCode;

            // Strip line number prefixes for compilation
            var codeForCompilation = TranspilerFactory.StripLineNumberPrefixes(generatedCode);

            var syntaxTree = CSharpSyntaxTree.ParseText(codeForCompilation);
            var compilation = CSharpCompilation.Create("MarathonCompilation")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddSyntaxTrees(syntaxTree);

            var diagnostics = compilation.GetDiagnostics();
            return MapDiagnosticsToMarathonCode(diagnostics);
        }

        private List<CompilationError> MapDiagnosticsToMarathonCode(IEnumerable<Diagnostic> diagnostics)
        {
            var errors = new List<CompilationError>();

            foreach (var diagnostic in diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
            {
                var lineSpan = diagnostic.Location.GetLineSpan();
                var errorLine = lineSpan.StartLinePosition.Line;

                // Find the line in the original code with line number prefix
                var linePrefixMatch = Regex.Match(GetLineFromSource(errorLine), @"^\s*(\d+):");
                if (linePrefixMatch.Success)
                {
                    int originalLine = int.Parse(linePrefixMatch.Groups[1].Value);

                    errors.Add(new CompilationError
                    {
                        Message = diagnostic.GetMessage(),
                        MarathonLine = originalLine,
                        Severity = diagnostic.Severity.ToString()
                    });
                }
            }

            return errors;
        }

        private string GetLineFromSource(int lineNumber)
        {
            var lines = _generatedCode.Split('\n');
            if (lineNumber >= 0 && lineNumber < lines.Length)
            {
                return lines[lineNumber];
            }
            return string.Empty;
        }

        // Store the generated code for error mapping
        private string _generatedCode;
    }
}
