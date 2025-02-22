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
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using MediatR.Pipeline;
using System.Runtime.Serialization;

namespace MarathonTranspiler.LSP
{
    public class SemanticTokensHandler : SemanticTokensHandlerBase
    {
        private readonly Workspace _workspace;
        private readonly SemanticTokensLegend _legend;
        private readonly ConcurrentDictionary<string, List<SemanticTokenData>> _codeBlockCache = new ConcurrentDictionary<string, List<SemanticTokenData>>();

        // Cache for language mapping and configuration
        private static readonly ConcurrentDictionary<string, string> _targetLanguageCache = new ConcurrentDictionary<string, string>();
        private static readonly ConcurrentDictionary<string, ILanguage> _colorCodeLanguageCache = new ConcurrentDictionary<string, ILanguage>();

        // Precomputed token type mappings
        private static readonly Dictionary<string, SemanticTokenType> _tokenTypeMapping;
        private static readonly Dictionary<string, SemanticTokenModifier[]> _modifierMapping;

        static SemanticTokensHandler()
        {
            // Initialize mappings once for the application lifetime
            _tokenTypeMapping = InitializeTokenTypeMapping();
            _modifierMapping = InitializeModifierMapping();
        }

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
                        SemanticTokenType.Parameter,
                        SemanticTokenType.Type,
                        SemanticTokenType.Property
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
            return new SemanticTokensRegistrationOptions
            {
                DocumentSelector = new TextDocumentSelector(new TextDocumentFilter
                {
                    Pattern = "**/*.mrt",
                    Language = "mrt"
                }),
                Legend = this._legend,
                Full = new SemanticTokensCapabilityRequestFull
                {
                    Delta = true,
                },
                Range = false
            };
        }

        // Cache for tokenization results to avoid duplicate work
        private readonly ConcurrentDictionary<string, (DateTime timestamp, SemanticTokens tokens)> _tokenCache =
            new ConcurrentDictionary<string, (DateTime, SemanticTokens)>();

        private int _lineCount = 0;

        // Maximum age of cached tokens (10 seconds)
        private static readonly TimeSpan _cacheTimeout = TimeSpan.FromSeconds(10);

        public override async Task<SemanticTokensFullOrDelta?> Handle(SemanticTokensDeltaParams request, CancellationToken cancellationToken)
        {
            // Delta requests are always processed by base class
            // return await base.Handle(request, cancellationToken);

            var identifier = request.TextDocument;
            var uri = identifier.Uri;
            string docKey = request.TextDocument.Uri.ToString();

            // Check if we have a recent cached result
            if (_tokenCache.TryGetValue(docKey, out var cachedFirst) &&
                (DateTime.UtcNow - cachedFirst.timestamp) < _cacheTimeout)
            {
                return GetEmptyDelta();
            }

            var document = new SemanticTokensDocument(this._legend);
            var builder = document.Create();

            await Tokenize(builder, request, cancellationToken);

            var semanticTokens = builder.Commit().GetSemanticTokens();

            // Check if we have a cached result
            if (_tokenCache.TryGetValue(docKey, out var cached))
            {
                if (semanticTokens != null)
                {
                    var length = semanticTokens.Data.Length;
                    var cachedLength = cached.tokens.Data.Length;
                    string[] lines = _workspace.GetDocumentLines(uri);

                    if (length == cachedLength && lines.Length == _lineCount)
                    {
                        _lineCount = lines.Length;
                        return GetEmptyDelta();
                    }
                    else if (semanticTokens != null)
                    {
                        _lineCount = lines.Length;
                        _tokenCache[docKey] = (DateTime.UtcNow, semanticTokens);
                    }
                    else
                    {
                        _lineCount = lines.Length;
                    }
                }
            }

            if (semanticTokens == null)
            {
                return GetEmptyDelta();
            }

            var full = new SemanticTokensFullOrDelta(semanticTokens);

            return full;
        }

        public SemanticTokensFullOrDelta GetEmptyDelta()
        {
            List<SemanticTokensEdit> tokensEdit = new List<SemanticTokensEdit>();

            var pResult = new SemanticTokensDeltaPartialResult
            {
                Edits = Container<SemanticTokensEdit>.From(tokensEdit)
            };

            var semanticDelta = new SemanticTokensDelta(pResult);
            var sDelta = new SemanticTokensFullOrDelta(semanticDelta);

            return sDelta;
        }

        public override async Task<SemanticTokens?> Handle(SemanticTokensParams request, CancellationToken cancellationToken)
        {
            string docKey = request.TextDocument.Uri.ToString();

            // Check if we have a recent cached result
            if (_tokenCache.TryGetValue(docKey, out var cached) &&
                (DateTime.UtcNow - cached.timestamp) < _cacheTimeout)
            {
                return cached.tokens;
            }

            // Get fresh tokens
            var result = await base.Handle(request, cancellationToken);

            // Cache the result if we got one
            if (result != null)
            {
                _tokenCache[docKey] = (DateTime.UtcNow, result);
            }

            return result;
        }

        public override async Task<SemanticTokens?> Handle(SemanticTokensRangeParams request, CancellationToken cancellationToken)
        {
            string docKey = request.TextDocument.Uri.ToString();

            // Check if we have a recent cached result
            if (_tokenCache.TryGetValue(docKey, out var cached))
            {
                return cached.tokens;
            }

            // Range requests should be processed directly since they're for specific regions
            return await base.Handle(request, cancellationToken);
        }

        protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(ITextDocumentIdentifierParams @params, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SemanticTokensDocument(this._legend));
        }

        protected override Task Tokenize(SemanticTokensBuilder builder, ITextDocumentIdentifierParams identifier, CancellationToken cancellationToken)
        {
            // Early exit if cancelled
            if (cancellationToken.IsCancellationRequested)
                return Task.CompletedTask;

            var uri = identifier.TextDocument.Uri;
            string[] lines = _workspace.GetDocumentLines(uri);
            if (lines == null || lines.Length == 0)
            {
                return Task.CompletedTask;
            }

            // Get target language (with caching)
            var targetLanguage = GetTargetLanguageFromConfig(uri);
            var colorCodeLanguage = GetColorCodeLanguage(targetLanguage);
            var originalColorCodeLanguage = colorCodeLanguage;

            // Create a custom ColorCode formatter that builds semantic tokens
            var formatter = new SemanticTokenFormatter(builder, _tokenTypeMapping, _modifierMapping);

            // Parse Marathon annotations and code blocks
            var reader = new MarathonReader();
            var annotatedBlocks = reader.ParseFile(lines.ToList());

            int currentLineNumber = 0;

            for (int i = 0; i < lines.Length; ++i)
            {
                if (lines[i].Trim().Length == 0)
                {
                    currentLineNumber++;
                }
                else
                {
                    break;
                }
            }

            bool useCache = true;

            foreach (var block in annotatedBlocks)
            {
                // Check for cancellation frequently
                if (cancellationToken.IsCancellationRequested)
                    return Task.CompletedTask;

                // Handle annotations first
                if (block.Annotations.Any())
                {
                    var firstAnnotation = block.Annotations[0];
                    if (firstAnnotation.Name == "xml")
                    {
                        colorCodeLanguage = ColorCode.Languages.Xml;
                    }
                    else
                    {
                        colorCodeLanguage = originalColorCodeLanguage;
                    }

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
                }

                // Now handle the code block with language-specific highlighting
                if (block.Code.Count > 0)
                {
                    string codeText = string.Join(Environment.NewLine, block.RawCode);
                    string blockHash = ComputeHash(codeText);

                    if (useCache && _codeBlockCache.TryGetValue(blockHash, out var cachedTokens))
                    {
                        if (cachedTokens.Any(x => !lines[x.Line].Contains(x.Token)))
                        {
                            useCache = false;
                            CacheMiss(builder, formatter, codeText, colorCodeLanguage, blockHash, currentLineNumber);
                        }
                        else
                        {
                            // Apply cached tokens
                            foreach (var token in cachedTokens)
                            {
                                builder.Push(
                                    token.Line,
                                    token.Column,
                                    token.Length,
                                    token.TokenType,
                                    token.Modifiers
                                );
                            }
                        }
                    }
                    else
                    {
                        useCache = false;
                        CacheMiss(builder, formatter, codeText, colorCodeLanguage, blockHash, currentLineNumber);
                    }

                    // Update line position
                    currentLineNumber += block.RawCode.Count;
                }
            }

            return Task.CompletedTask;
        }

        private void CacheMiss(SemanticTokensBuilder builder, SemanticTokenFormatter formatter, string codeText, ILanguage colorCodeLanguage, string blockHash, int currentLineNumber)
        {
            // Cache miss or we're no longer using cache, process the block
            List<SemanticTokenData> blockTokens = new List<SemanticTokenData>();

            // Set the current line offset in the formatter
            formatter.CurrentTokens = blockTokens;

            // Format the code
            formatter.Format(codeText, colorCodeLanguage, currentLineNumber);

            // Cache the tokens for this block
            _codeBlockCache[blockHash] = blockTokens;

            // Apply the tokens to the builder
            foreach (var token in blockTokens)
            {
                builder.Push(
                    token.Line,
                    token.Column,
                    token.Length,
                    token.TokenType,
                    token.Modifiers
                );
            }
        }

        private string ComputeHash(string text)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
                return Convert.ToBase64String(bytes);
            }
        }

        // Optimized ColorCode formatter that builds semantic tokens
        private class SemanticTokenFormatter : CodeColorizerBase
        {
            private readonly SemanticTokensBuilder _builder;
            private readonly Dictionary<string, SemanticTokenType> _tokenTypeMapping;
            private readonly Dictionary<string, SemanticTokenModifier[]> _modifierMapping;
            private int[] _lineStarts;

            public int LineOffset { get; set; } = 0;

            public int GlobalColumnOffset { get; set; } = 0;

            public int CurrentLine { get; set; } = 0;

            public List<SemanticTokenData> CurrentTokens { get; set; }

            public SemanticTokenFormatter(
                SemanticTokensBuilder builder,
                Dictionary<string, SemanticTokenType> tokenTypeMapping,
                Dictionary<string, SemanticTokenModifier[]> modifierMapping)
                : base(StyleDictionary.DefaultLight, null)
            {
                _builder = builder;
                _tokenTypeMapping = tokenTypeMapping;
                _modifierMapping = modifierMapping;
            }

            public void Format(string sourceCode, ILanguage language, int startLineNumber)
            {
                this.CurrentLine = startLineNumber;

                // Use the language parser from the base class
                languageParser.Parse(sourceCode, language, (parsedCode, scopes) => Write(parsedCode, scopes));
            }

            protected override void Write(string parsedSourceCode, IList<Scope> scopes)
            {       
                if (scopes.Count == 0)
                {
                    var split = parsedSourceCode.Split("\r\n").ToList();
                    if (split.Count > 1)
                    {
                        this.CurrentLine++;


                        if (split.Count > 2)
                        {
                            this.CurrentLine += (split.Count - 2);
                        }

                        var l = parsedSourceCode.Replace("\r\n", "").Replace("\n", "");
                        var tl = l.TrimEnd();
                        var diff = l.Length - tl.Length;
                        this.GlobalColumnOffset = diff;
                    }
                    else
                    {
                        this.GlobalColumnOffset += parsedSourceCode.Length;
                    }
                }
                else
                {
                    // Process scopes to create semantic tokens
                    ProcessScopes(scopes, parsedSourceCode);
                    this.GlobalColumnOffset += parsedSourceCode.Length;
                }
            }

            private void ProcessScopes(IList<Scope> scopes, string sourceCode)
            {
                // Use stack-based iteration instead of recursion for better performance
                var scopeStack = new Stack<Scope>(scopes.Reverse());

                while (scopeStack.Count > 0)
                {
                    var scope = scopeStack.Pop();

                    // Process current scope
                    if (!string.IsNullOrEmpty(scope.Name))
                    {
                        ProcessScope(scope, sourceCode);
                    }

                    // Push children in reverse order so they get processed in forward order
                    if (scope.Children.Count > 0)
                    {
                        for (int i = scope.Children.Count - 1; i >= 0; i--)
                        {
                            scopeStack.Push(scope.Children[i]);
                        }
                    }
                }
            }

            private void ProcessScope(Scope scope, string sourceCode)
            {
                // Get token type (using cached mapping for better performance)
                var tokenType = _tokenTypeMapping.TryGetValue(scope.Name.ToLowerInvariant(), out var type)
                    ? type
                    : SemanticTokenType.Variable;

                // Get modifiers (using cached mapping)
                var modifiers = _modifierMapping.TryGetValue(scope.Name.ToLowerInvariant(), out var mods)
                    ? mods
                    : Array.Empty<SemanticTokenModifier>();

                // Get text for this scope
                int start = scope.Index;
                int length = scope.Length;

                // Avoid substring allocations for very large texts
                if (start >= sourceCode.Length)
                    return;

                // Adjust length if it would go past the end of the string
                if (start + length > sourceCode.Length)
                {
                    length = sourceCode.Length - start;
                }

                // Handle multi-line tokens more efficiently
                int currentPos = start;
                int currentLine = 0;
                int currentColumn = currentPos;

                int end = start + length;
                while (currentPos < end)
                {
                    // Find end of current line or end of token
                    int lineEnd = FindNextLineBreak(sourceCode, currentPos, end);
                    int tokenLengthInLine = lineEnd - currentPos;

                    if (tokenLengthInLine > 0)
                    {
                        var tokenData = new SemanticTokenData
                        {
                            Line = this.CurrentLine + currentLine,
                            Column = this.GlobalColumnOffset + currentColumn,
                            Length = tokenLengthInLine,
                            Token = sourceCode,
                            TokenType = tokenType,
                            Modifiers = modifiers
                        };

                        CurrentTokens.Add(tokenData);
                    }

                    if (lineEnd < end)
                    {
                        // Move to next line
                        currentPos = lineEnd + 1; // Skip newline character
                        currentLine++;
                        currentColumn = 0;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            private int FindNextLineBreak(string text, int start, int end)
            {
                for (int i = start; i < end; i++)
                {
                    if (text[i] == '\n' || text[i] == '\r')
                    {
                        // Skip \r\n sequence
                        if (text[i] == '\r' && i + 1 < end && text[i + 1] == '\n')
                        {
                            return i;
                        }
                        return i;
                    }
                }
                return end;
            }
        }

        private int FindAnnotationLine(string[] lines, Annotation annotation, int startLineNumber)
        {
            for (int i = startLineNumber; i < lines.Length; i++)
            {
                // Use faster string check
                string trimmedLine = lines[i].TrimStart();
                if (trimmedLine.Length > annotation.Name.Length + 2 &&
                    trimmedLine[0] == '@' &&
                    trimmedLine.AsSpan(1, annotation.Name.Length).Equals(annotation.Name.AsSpan(), StringComparison.Ordinal) &&
                    trimmedLine[annotation.Name.Length + 1] == '(')
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
                    string valuePattern = $"\"{kvp.Value}\"";
                    int valueIndex = line.IndexOf(valuePattern, keyIndex);
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

        private ILanguage GetColorCodeLanguage(string targetLanguage)
        {
            // Check cache first
            if (_colorCodeLanguageCache.TryGetValue(targetLanguage, out var language))
            {
                return language;
            }

            // Map and cache the result
            language = MapToColorCodeLanguage(targetLanguage);
            _colorCodeLanguageCache[targetLanguage] = language;
            return language;
        }

        private ILanguage MapToColorCodeLanguage(string targetLanguage)
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
            // Create a cache key from the directory path
            var filePath = documentUri.GetFileSystemPath();
            var directory = Path.GetDirectoryName(filePath);

            // Check cache first
            if (_targetLanguageCache.TryGetValue(directory, out var cachedLanguage))
            {
                return cachedLanguage;
            }

            try
            {
                // Look for mrtconfig.json in the same directory
                var configPath = Path.Combine(directory, "mrtconfig.json");

                if (File.Exists(configPath))
                {
                    var configJson = File.ReadAllText(configPath);
                    var config = Newtonsoft.Json.Linq.JObject.Parse(configJson);

                    // Use JSON path to get the target language
                    var targetLanguage = config.SelectToken("$.target")?.ToString();

                    if (string.IsNullOrEmpty(targetLanguage))
                    {
                        // Try alternative paths if needed
                        targetLanguage = config.SelectToken("$.transpilerOptions.target")?.ToString() ??
                                        config.SelectToken("$.settings.language")?.ToString();
                    }

                    if (!string.IsNullOrEmpty(targetLanguage))
                    {
                        // Cache the result
                        _targetLanguageCache[directory] = targetLanguage;
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
            var defaultLanguage = "csharp";
            _targetLanguageCache[directory] = defaultLanguage;
            return defaultLanguage;
        }

        // Pre-compute token type mappings
        private static Dictionary<string, SemanticTokenType> InitializeTokenTypeMapping()
        {
            var mapping = new Dictionary<string, SemanticTokenType>(StringComparer.OrdinalIgnoreCase)
            {
                { "keyword", SemanticTokenType.Keyword },
                { "datakeyword", SemanticTokenType.Keyword },
                { "preprocessorkeyword", SemanticTokenType.Keyword },
                { "controlkeyword", SemanticTokenType.Keyword },
                { "operatorkeyword", SemanticTokenType.Keyword },
                { "typekeyword", SemanticTokenType.Keyword },
                { "visibilitykeyword", SemanticTokenType.Keyword },

                { "comment", SemanticTokenType.Comment },
                { "xmldoccomment", SemanticTokenType.Comment },
                { "xmlcomment", SemanticTokenType.Comment },
                { "comment.line", SemanticTokenType.Comment },
                { "comment.block", SemanticTokenType.Comment },
                { "preprocessordirective", SemanticTokenType.Comment },

                { "string", SemanticTokenType.String },
                { "stringescape", SemanticTokenType.String },
                { "characterliteral", SemanticTokenType.String },
                { "verbatimstring", SemanticTokenType.String },

                { "class", SemanticTokenType.Class },
                { "interface", SemanticTokenType.Class },
                { "enum", SemanticTokenType.Class },
                { "structure", SemanticTokenType.Class },
                { "typename", SemanticTokenType.Class },
                { "delegate", SemanticTokenType.Class },
                { "type", SemanticTokenType.Class },

                { "method", SemanticTokenType.Function },
                { "methodname", SemanticTokenType.Function },
                { "constructor", SemanticTokenType.Function },
                { "destructor", SemanticTokenType.Function },
                { "function", SemanticTokenType.Function },
                { "functionname", SemanticTokenType.Function },

                { "number", SemanticTokenType.Number },
                { "digit", SemanticTokenType.Number },
                { "integer", SemanticTokenType.Number },
                { "hex", SemanticTokenType.Number },
                { "octal", SemanticTokenType.Number },
                { "binary", SemanticTokenType.Number },
                { "integerliteral", SemanticTokenType.Number },
                { "decimalliteral", SemanticTokenType.Number },
                { "floatingpointliteral", SemanticTokenType.Number },

                { "xmlattribute", SemanticTokenType.Parameter },
                { "xmlattributequotes", SemanticTokenType.Parameter },
                { "xmlattributevalue", SemanticTokenType.Parameter },
                { "attribute", SemanticTokenType.Parameter },
                { "attributename", SemanticTokenType.Parameter },
                { "htmlattribute", SemanticTokenType.Parameter },
                { "htmlattributename", SemanticTokenType.Parameter },
                { "htmlattributevalue", SemanticTokenType.Parameter },
                { "cssselectorclass", SemanticTokenType.Parameter },
                { "cssselectorid", SemanticTokenType.Parameter },
                { "parameter", SemanticTokenType.Parameter },

                { "constant", SemanticTokenType.Type },
                { "null", SemanticTokenType.Type },
                { "boolean", SemanticTokenType.Type },
                { "predefined-type", SemanticTokenType.Type },
                { "xml name", SemanticTokenType.Type },

                { "xml attribute", SemanticTokenType.Property },

                { "variable", SemanticTokenType.Variable },
                { "variablename", SemanticTokenType.Variable },
                { "identifier", SemanticTokenType.Variable },
                { "localvariable", SemanticTokenType.Variable },
                { "instance", SemanticTokenType.Variable }
            };

            return mapping;
        }

        private static Dictionary<string, SemanticTokenModifier[]> InitializeModifierMapping()
        {
            var result = new Dictionary<string, SemanticTokenModifier[]>(StringComparer.OrdinalIgnoreCase);

            // Add common modifiers
            result["static"] = new[] { SemanticTokenModifier.Static };
            result["readonly"] = new[] { SemanticTokenModifier.Readonly };
            result["constant"] = new[] { SemanticTokenModifier.Readonly, SemanticTokenModifier.Static };
            result["abstract"] = new[] { SemanticTokenModifier.Abstract };
            result["async"] = new[] { SemanticTokenModifier.Async };
            result["deprecated"] = new[] { SemanticTokenModifier.Deprecated };
            result["obsolete"] = new[] { SemanticTokenModifier.Deprecated };
            result["declaration"] = new[] { SemanticTokenModifier.Declaration };
            result["definition"] = new[] { SemanticTokenModifier.Declaration };

            // Documentation modifiers for comments
            foreach (var commentType in new[] { "comment", "xmldoc", "xmldoccomment", "comment.line", "comment.block" })
            {
                result[commentType] = new[] { SemanticTokenModifier.Documentation };
            }

            // Default library modifiers
            foreach (var builtinType in new[] { "builtin", "predefined" })
            {
                result[builtinType] = new[] { SemanticTokenModifier.DefaultLibrary };
            }

            return result;
        }
    }
}