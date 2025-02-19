using ColorCode.Parsing;
using ColorCode.Styling;
using ColorCode;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.LSP
{
    public class SemanticTokensHandler : SemanticTokensHandlerBase
    {
        private readonly Workspace _workspace;
        private readonly SemanticTokensLegend _legend;

        public SemanticTokensHandler(Workspace workspace)
        {
            _workspace = workspace;
            _legend = new SemanticTokensLegend
            {
                TokenTypes = new Container<SemanticTokenType>(
                        SemanticTokenType.Class,
                        SemanticTokenType.Function,
                        SemanticTokenType.Variable,
                        SemanticTokenType.String,
                        SemanticTokenType.Number,
                        SemanticTokenType.Keyword,
                        SemanticTokenType.Comment,
                        SemanticTokenType.Parameter
                    ),
                TokenModifiers = new Container<SemanticTokenModifier>(
                        SemanticTokenModifier.Declaration,
                        SemanticTokenModifier.Definition,
                        SemanticTokenModifier.Readonly
                    )
            };
        }

        protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(SemanticTokensCapability capability, ClientCapabilities clientCapabilities)
        {
            // this._workspace.SendNotification("Registering...");

            var options = new SemanticTokensRegistrationOptions
            {
                DocumentSelector = new TextDocumentSelector(new TextDocumentFilter
                {
                    Pattern = "**/*.mrt",
                    Language = "mrt"
                }),
                Legend = this._legend,
                Full = new SemanticTokensCapabilityRequestFull
                {
                    Delta = false
                },
                Range = true
            };

            return options;
        }

        public override Task<SemanticTokensFullOrDelta?> Handle(SemanticTokensDeltaParams request, CancellationToken cancellationToken)
        {
            return base.Handle(request, cancellationToken);
        }

        public override Task<SemanticTokens?> Handle(SemanticTokensParams request, CancellationToken cancellationToken)
        {
            return base.Handle(request, cancellationToken);
        }

        public override Task<SemanticTokens?> Handle(SemanticTokensRangeParams request, CancellationToken cancellationToken)
        {
            return base.Handle(request, cancellationToken);
        }

        protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(ITextDocumentIdentifierParams @params, CancellationToken cancellationToken)
        {
            // this._workspace.SendNotification("Get Document....");
            return Task.FromResult(new SemanticTokensDocument(this._legend));
        }

        protected override Task Tokenize(SemanticTokensBuilder builder, ITextDocumentIdentifierParams identifier, CancellationToken cancellationToken)
        {
            string text = _workspace.GetDocumentText(identifier.TextDocument.Uri);
            if (string.IsNullOrEmpty(text))
            {
                return Task.CompletedTask;
            }

            string[] lines = _workspace.GetDocumentLines(identifier.TextDocument.Uri);
            if (lines == null || lines.Length == 0)
            {
                return Task.CompletedTask;
            }

            // Get target language from mrtconfig.json
            var targetLanguage = GetTargetLanguageFromConfig(identifier.TextDocument.Uri);
            var colorCodeLanguage = MapToColorCodeLanguage(targetLanguage);

            // Create a custom ColorCode formatter that builds semantic tokens
            var formatter = new SemanticTokenFormatter(builder);

            // Parse Marathon annotations and code blocks
            var reader = new MarathonReader();
            var annotatedBlocks = reader.ParseFile(lines.ToList());

            int currentLineNumber = 0;

            foreach (var block in annotatedBlocks)
            {
                // Handle annotations first
                foreach (var annotation in block.Annotations)
                {
                    // Find the line containing this annotation
                    int annotationLineNumber = FindAnnotationLine(lines, annotation, currentLineNumber);
                    if (annotationLineNumber >= 0)
                    {
                        // Highlight the annotation structure
                        HighlightAnnotation(builder, lines[annotationLineNumber], annotation, annotationLineNumber);
                        currentLineNumber = annotationLineNumber + 1;
                    }
                }

                // Now handle the code block with language-specific highlighting
                if (block.Code.Count > 0)
                {
                    var codeText = string.Join(Environment.NewLine, block.Code);

                    // Set the current line offset in the formatter
                    formatter.LineOffset = currentLineNumber;

                    // Use ColorCode's existing parser and our custom formatter to build semantic tokens
                    formatter.Format(codeText, colorCodeLanguage);

                    // Update line position
                    currentLineNumber += block.Code.Count;
                }
            }

            return Task.CompletedTask;
        }

        // Custom ColorCode formatter that builds semantic tokens
        private class SemanticTokenFormatter : CodeColorizerBase
        {
            private readonly SemanticTokensBuilder _builder;
            private int[] _lineStarts;

            public int LineOffset { get; set; } = 0;

            public SemanticTokenFormatter(SemanticTokensBuilder builder)
                : base(StyleDictionary.DefaultLight, null) // Use default styles and let base create parser
            {
                _builder = builder;
            }

            public void Format(string sourceCode, ILanguage language)
            {
                // Calculate line starts for position mapping
                _lineStarts = CalculateLineStarts(sourceCode);

                // Use the language parser from the base class
                languageParser.Parse(sourceCode, language, (parsedCode, scopes) => Write(parsedCode, scopes));
            }

            protected override void Write(string parsedSourceCode, IList<Scope> scopes)
            {
                // Process scopes to create semantic tokens
                ProcessScopes(scopes, parsedSourceCode);
            }

            private void ProcessScopes(IList<Scope> scopes, string sourceCode)
            {
                foreach (var scope in scopes)
                {
                    // Process this scope
                    ProcessScope(scope, sourceCode);

                    // Process child scopes
                    if (scope.Children.Count > 0)
                    {
                        ProcessScopes(scope.Children, sourceCode);
                    }
                }
            }

            private void ProcessScope(Scope scope, string sourceCode)
            {
                if (string.IsNullOrEmpty(scope.Name))
                    return;

                // Map scope name to token type
                var tokenType = MapScopeNameToTokenType(scope.Name);

                // Determine appropriate modifiers for this scope
                var modifiers = DetermineTokenModifiers(scope.Name);

                // Get the text for this scope
                string tokenText = sourceCode.Substring(scope.Index, scope.Length);

                // Handle multi-line tokens
                string[] tokenLines = tokenText.Split('\n');
                for (int i = 0; i < tokenLines.Length; i++)
                {
                    if (string.IsNullOrEmpty(tokenLines[i]))
                        continue;

                    // Get position information
                    (int startLine, int startColumn) = GetLineAndColumn(scope.Index +
                        (i == 0 ? 0 : tokenText.IndexOf(tokenLines[i], scope.Index)), _lineStarts);

                    _builder.Push(
                        startLine + LineOffset,
                        startColumn,
                        tokenLines[i].Length,
                        tokenType,
                        modifiers
                    );
                }
            }

            private SemanticTokenModifier[] DetermineTokenModifiers(string scopeName)
            {
                var modifiers = new List<SemanticTokenModifier>();
                string lowerScopeName = scopeName.ToLowerInvariant();

                // Add modifiers based on scope name patterns
                if (lowerScopeName.Contains("static") || lowerScopeName == "constant")
                {
                    modifiers.Add(SemanticTokenModifier.Static);
                }

                if (lowerScopeName.Contains("readonly") || lowerScopeName == "constant")
                {
                    modifiers.Add(SemanticTokenModifier.Readonly);
                }

                if (lowerScopeName.Contains("abstract"))
                {
                    modifiers.Add(SemanticTokenModifier.Abstract);
                }

                if (lowerScopeName.Contains("async"))
                {
                    modifiers.Add(SemanticTokenModifier.Async);
                }

                if (lowerScopeName.Contains("deprecated") || lowerScopeName.Contains("obsolete"))
                {
                    modifiers.Add(SemanticTokenModifier.Deprecated);
                }

                if (lowerScopeName.Contains("declaration") || lowerScopeName.Contains("definition"))
                {
                    modifiers.Add(SemanticTokenModifier.Declaration);
                }

                // Add documentation modifier for comments
                if (lowerScopeName.Contains("comment") || lowerScopeName.Contains("xmldoc"))
                {
                    modifiers.Add(SemanticTokenModifier.Documentation);
                }

                // Add defaultLibrary modifier for standard library types/functions
                if (lowerScopeName.Contains("builtin") || lowerScopeName.Contains("predefined"))
                {
                    modifiers.Add(SemanticTokenModifier.DefaultLibrary);
                }

                return modifiers.ToArray();
            }

            private int[] CalculateLineStarts(string text)
            {
                var lineStarts = new List<int> { 0 }; // First line starts at index 0

                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] == '\n')
                    {
                        lineStarts.Add(i + 1); // Next line starts after the newline
                    }
                }

                return lineStarts.ToArray();
            }

            private (int line, int column) GetLineAndColumn(int position, int[] lineStarts)
            {
                // Find the line containing this position
                for (int i = 0; i < lineStarts.Length; i++)
                {
                    if (i == lineStarts.Length - 1 || position < lineStarts[i + 1])
                    {
                        return (i, position - lineStarts[i]);
                    }
                }

                // Fallback (should not happen with valid input)
                return (0, position);
            }

            private SemanticTokenType MapScopeNameToTokenType(string scopeName)
            {
                switch (scopeName.ToLowerInvariant())
                {
                    case "keyword":
                    case "datakeyword":
                    case "preprocessorkeyword":
                    case "controlkeyword":
                    case "operatorkeyword":
                    case "typekeyword":
                    case "visibilitykeyword":
                        return SemanticTokenType.Keyword;

                    case "comment":
                    case "xmldoccomment":
                    case "xmlcomment":
                    case "comment.line":
                    case "comment.block":
                    case "preprocessordirective":
                        return SemanticTokenType.Comment;

                    case "string":
                    case "stringescape":
                    case "characterliteral":
                    case "verbatimstring":
                        return SemanticTokenType.String;

                    case "class":
                    case "interface":
                    case "enum":
                    case "structure":
                    case "typename":
                    case "delegate":
                    case "type":
                        return SemanticTokenType.Class;

                    case "method":
                    case "methodname":
                    case "constructor":
                    case "destructor":
                    case "function":
                    case "functionname":
                        return SemanticTokenType.Function;

                    case "number":
                    case "digit":
                    case "integer":
                    case "hex":
                    case "octal":
                    case "binary":
                    case "integerliteral":
                    case "decimalliteral":
                    case "floatingpointliteral":
                        return SemanticTokenType.Number;

                    case "xmlattribute":
                    case "xmlattributequotes":
                    case "xmlattributevalue":
                    case "attribute":
                    case "attributename":
                    case "htmlattribute":
                    case "htmlattributename":
                    case "htmlattributevalue":
                    case "cssselectorclass":
                    case "cssselectorid":
                    case "parameter":
                        return SemanticTokenType.Parameter;

                    case "constant":
                    case "null":
                    case "boolean":
                    case "predefined-type":
                        return SemanticTokenType.Type;

                    case "variable":
                    case "variablename":
                    case "identifier":
                    case "localvariable":
                    case "instance":
                        return SemanticTokenType.Variable;

                    default:
                        return SemanticTokenType.Variable;
                }
            }
        }

        private int FindAnnotationLine(string[] lines, Annotation annotation, int startLineNumber)
        {
            for (int i = startLineNumber; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith($"@{annotation.Name}("))
                {
                    return i;
                }
            }
            return -1;
        }

        private void HighlightAnnotation(SemanticTokensBuilder builder, string line, Annotation annotation, int lineNumber)
        {
            // Highlight the annotation name (e.g., @varInit)
            int annotationStart = line.IndexOf('@');
            if (annotationStart >= 0)
            {
                builder.Push(
                    lineNumber,
                    annotationStart + 1, // Skip the @ symbol
                    annotation.Name.Length,
                    SemanticTokenType.Keyword,
                    SemanticTokenModifier.Declaration
                );
            }

            // Highlight the key-value pairs
            foreach (var kvp in annotation.Values)
            {
                // Find and highlight the key
                int keyIndex = line.IndexOf(kvp.Key, annotationStart);
                if (keyIndex >= 0)
                {
                    builder.Push(
                        lineNumber,
                        keyIndex,
                        kvp.Key.Length,
                        SemanticTokenType.Parameter,
                        SemanticTokenModifier.Defaults
                    );

                    // Find and highlight the value (as a string)
                    int valueIndex = line.IndexOf($"\"{kvp.Value}\"", keyIndex);
                    if (valueIndex >= 0)
                    {
                        builder.Push(
                            lineNumber,
                            valueIndex,
                            kvp.Value.Length + 2, // +2 for the quotes
                            SemanticTokenType.String,
                            SemanticTokenModifier.Defaults
                        );
                    }
                }
            }
        }

        private ColorCode.ILanguage MapToColorCodeLanguage(string targetLanguage)
        {
            switch (targetLanguage.ToLowerInvariant())
            {
                case "csharp":
                case "cs":
                    return ColorCode.Languages.CSharp;
                case "javascript":
                case "js":
                    return ColorCode.Languages.JavaScript;
                case "typescript":
                case "ts":
                    return ColorCode.Languages.Typescript;
                case "html":
                    return ColorCode.Languages.Html;
                case "css":
                    return ColorCode.Languages.Css;
                case "xml":
                    return ColorCode.Languages.Xml;
                case "php":
                    return ColorCode.Languages.Php;
                case "sql":
                    return ColorCode.Languages.Sql;
                case "powershell":
                    return ColorCode.Languages.PowerShell;
                case "vb":
                case "vbnet":
                    return ColorCode.Languages.VbDotNet;
                case "koka":
                    return ColorCode.Languages.Koka;
                case "markdown":
                case "md":
                    return ColorCode.Languages.Markdown;
                case "cpp":
                case "c++":
                    return ColorCode.Languages.Cpp;
                case "fsharp":
                case "fs":
                    return ColorCode.Languages.FSharp;
                case "java":
                    return ColorCode.Languages.Java;
                default:
                    // Default to C# if language not supported
                    return ColorCode.Languages.CSharp;
            }
        }

        private string GetTargetLanguageFromConfig(DocumentUri documentUri)
        {
            try
            {
                // Get directory containing the .mrt file
                var filePath = documentUri.GetFileSystemPath();
                var directory = Path.GetDirectoryName(filePath);

                // Look for mrtconfig.json in the same directory
                var configPath = Path.Combine(directory, "mrtconfig.json");

                if (File.Exists(configPath))
                {
                    var configJson = File.ReadAllText(configPath);
                    var config = Newtonsoft.Json.Linq.JObject.Parse(configJson);

                    // Use JSON path to get the target language
                    // This supports nested properties like "compiler.targetLanguage"
                    var targetLanguage = config.SelectToken("$.target")?.ToString();

                    // If the path is different, you can change it accordingly
                    // For example: "$.compiler.language" or "$.settings.target"
                    if (string.IsNullOrEmpty(targetLanguage))
                    {
                        // Try alternative paths if needed
                        targetLanguage = config.SelectToken("$.transpilerOptions.target")?.ToString() ??
                                        config.SelectToken("$.settings.language")?.ToString();
                    }

                    if (!string.IsNullOrEmpty(targetLanguage))
                    {
                        return targetLanguage;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error but continue with default
                Console.Error.WriteLine($"Error reading mrtconfig.json: {ex.Message}");
            }

            // Default to C# if config file not found or invalid
            return "csharp";
        }
    }
}
