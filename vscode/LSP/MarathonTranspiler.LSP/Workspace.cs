﻿using System;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using System.Collections.Concurrent;
using MarathonTranspiler.LSP.Extensions;
using System.Reflection;
using MarathonTranspiler.Model;
using MarathonTranspiler.Transpilers.CSharp;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using MarathonTranspiler.Core;
using System.Text.Json;
using MarathonTranspiler.LSP.Services;
using MarathonTranspiler.LSP.Model;
using MarathonTranspiler.Readers;
using System.Text.RegularExpressions;

namespace MarathonTranspiler.LSP
{
    public class Workspace
    {
        private readonly ConcurrentDictionary<DocumentUri, string> _documents = new();
        private readonly ConcurrentDictionary<DocumentUri, string[]> _documentLines = new();

        private readonly ConcurrentDictionary<DocumentUri, CancellationTokenSource> _compilationTokenSources = new();
        private readonly ConcurrentDictionary<DocumentUri, DateTime> _lastEditTimes = new();

        private static readonly ConcurrentDictionary<string, string> _targetLanguageCache = new ConcurrentDictionary<string, string>();
        private ILanguageServer _server;
        private string _rootPath;
        private StaticMethodRegistry? _registry;

        public void Initialize(ILanguageServer server, string rootPath)
        {
            _server = server;
            _rootPath = rootPath;
        }

        public void UpdateDocument(DocumentUri uri, string text)
        {
            if (_registry == null)
            {
                _registry = new StaticMethodRegistry();
                var fileInfo = new FileInfo(uri.ToUri().AbsolutePath);
                _registry.Initialize(Path.Combine(fileInfo.DirectoryName!, "lib"));
                _registry.SetTargetLanguage(GetTargetLanguageFromConfig(uri));
            }
            else
            {
                // Check if we need to update the target language
                string targetLanguage = GetTargetLanguageFromConfig(uri);
                if (_registry.TargetLanguage != targetLanguage)
                {
                    _registry.SetTargetLanguage(targetLanguage);
                }
            }

            _documents[uri] = text;
            _documentLines[uri] = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            // Update the last edit time
            _lastEditTimes[uri] = DateTime.UtcNow;

            // Cancel any pending compilation for this document
            if (_compilationTokenSources.TryGetValue(uri, out var existingTokenSource))
            {
                existingTokenSource.Cancel();
                _compilationTokenSources.TryRemove(uri, out _);
            }

            // Get config to check for real-time compilation and delay
            var config = GetConfigForDocument(uri);
            if (config?.TranspilerOptions?.CSharp?.RealTimeCompilation == true)
            {
                // Create new cancellation token source
                var tokenSource = new CancellationTokenSource();
                _compilationTokenSources[uri] = tokenSource;

                // Schedule compilation after the configured delay
                int delayMs = config.TranspilerOptions.CSharp.CompilationDelayMs;
                Task.Delay(delayMs, tokenSource.Token)
                    .ContinueWith(t =>
                    {
                        if (!t.IsCanceled)
                        {
                            TriggerCompilation(uri);
                        }
                    }, TaskScheduler.Default);
            }
        }

        private void TriggerCompilation(DocumentUri uri)
        {
            try
            {
                // Remove the token source as we're now executing
                _compilationTokenSources.TryRemove(uri, out _);

                // Get the document text
                if (!_documentLines.TryGetValue(uri, out var documentText))
                    return;

                // Get config
                var config = GetConfigForDocument(uri);
                if (config == null)
                    return;

                // Perform transpilation
                var marathonReader = new MarathonTranspiler.Readers.MarathonReader();
                var annotatedCode = marathonReader.ParseFile(documentText.ToList());

                var transpiler = TranspilerFactory.CreateTranspiler(config.TranspilerOptions, _registry);
                TranspilerFactory.ProcessAnnotatedCode(transpiler, annotatedCode, true);
                var outputCode = transpiler.GenerateOutput();

                // Compile the generated code
                if (transpiler is CSharpTranspiler csharpTranspiler)
                {
                    var compiler = new BackgroundCompilationService(config.TranspilerOptions.CSharp);
                    var errors = compiler.CompileAsync(outputCode).Result;

                    // Update diagnostics in the editor
                    UpdateDiagnostics(uri, errors);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                Console.Error.WriteLine($"Error during compilation: {ex.Message}");
            }
        }

        public void UpdateDiagnostics(DocumentUri uri, List<CompilationError> errors)
        {
            var diagnostics = errors.Select(error => new Diagnostic
            {
                Message = error.Message,
                Range = new Range(
                    new Position(error.MarathonLine - 1, 0),  // Line numbers are 1-based
                    new Position(error.MarathonLine - 1, int.MaxValue)),
                Severity = DiagnosticSeverity.Error,
                Source = "Marathon C# Compiler"
            }).ToList();

            this.SendDiagnostics(uri, diagnostics);
        }

        private Config GetConfigForDocument(DocumentUri uri)
        {
            try
            {
                var filePath = uri.GetFileSystemPath();
                var directory = Path.GetDirectoryName(filePath);
                var configPath = Path.Combine(directory, "mrtconfig.json");

                if (File.Exists(configPath))
                {
                    string jsonContent = File.ReadAllText(configPath);
                    return JsonSerializer.Deserialize<Config>(jsonContent);
                }
            }
            catch
            {
                // Silently fail and return null
            }

            return null;
        }

        private string GetDocumentDirectory(DocumentUri uri)
        {
            var filePath = uri.GetFileSystemPath();
            return Path.GetDirectoryName(filePath);
        }

        public void RemoveDocument(DocumentUri uri)
        {
            _documents.TryRemove(uri, out _);
            _documentLines.TryRemove(uri, out _);
        }

        public string GetDocumentText(DocumentUri uri)
        {
            return _documents.TryGetValue(uri, out var text) ? text : null;
        }

        public IEnumerable<DocumentUri> GetDocumentUris()
        {
            return _documents.Keys;
        }

        public string[] GetDocumentLines(DocumentUri uri)
        {
            return _documentLines.TryGetValue(uri, out var lines) ? lines : null;
        }

        public void SendDiagnostics(DocumentUri uri, IEnumerable<Diagnostic> diagnostics)
        {
            _server?.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
            {
                Uri = uri,
                Diagnostics = new Container<Diagnostic>(diagnostics)
            });
        }

        public void SendNotification(string message)
        {
            _server?.Window.ShowMessage(new ShowMessageParams
            {
                Type = MessageType.Info,
                Message = message
            });
        }

        // Additional utility methods for working with documents
        public void AnalyzeWorkspace()
        {
            foreach (var uri in _documents.Keys)
            {
                AnalyzeDocument(uri);
            }
        }

        public async Task<TranspiledCodeResponse> GetTranspiledCode(MarathonCodeParams parameters, CancellationToken token)
        {
            try
            {
                var uri = DocumentUri.Parse(parameters.Uri);

                // Get the document lines
                var lines = GetDocumentLines(uri);
                if (lines == null)
                    return new TranspiledCodeResponse { Code = "// No document content available" };

                // Load config to determine target language
                var config = GetConfigForDocument(uri);
                if (config == null)
                    return new TranspiledCodeResponse { Code = "// Unable to load configuration" };

                _registry!.SetTargetLanguage(GetTargetLanguageFromConfig(uri));

                // Process the document
                var marathonReader = new MarathonReader();
                var annotatedCode = marathonReader.ParseFile(lines.ToList());

                // Create transpiler and generate code
                var transpiler = TranspilerFactory.CreateTranspiler(config.TranspilerOptions, _registry);
                TranspilerFactory.ProcessAnnotatedCode(transpiler, annotatedCode, false);
                var outputCode = transpiler.GenerateOutput();

                var processedOutputCode = TranspilerFactory.StripLineNumberPrefixes(outputCode);

                return new TranspiledCodeResponse
                {
                    Code = processedOutputCode,
                    Target = config.TranspilerOptions.Target
                };
            }
            catch (Exception ex)
            {
                return new TranspiledCodeResponse
                {
                    Code = $"// Error generating transpiled code: {ex.Message}",
                    Target = "plaintext"
                };
            }
        }

        private void AnalyzeDocument(DocumentUri uri)
        {
            var lines = GetDocumentLines(uri);
            if (lines == null) return;

            // Analyze document content, e.g., extract class and function declarations,
            // validate the overall structure and relationships, etc.
            // This could involve parsing annotations and building a semantic model
        }

        // Track all class declarations across workspace
        public Dictionary<string, DocumentUri> GetClassDeclarations()
        {
            var declarations = new Dictionary<string, DocumentUri>();

            foreach (var (uri, lines) in _documentLines)
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].TrimStart().StartsWith("@varInit"))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(lines[i], @"className=""([^""]+)""");
                        if (match.Success)
                        {
                            var className = match.Groups[1].Value;
                            declarations[className] = uri;
                        }
                    }
                }
            }

            return declarations;
        }

        // Track all function declarations for a specific class
        public List<string> GetFunctionDeclarations(string className)
        {
            var functions = new List<string>();

            foreach (var lines in _documentLines.Values)
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].TrimStart().StartsWith("@run"))
                    {
                        var classMatch = System.Text.RegularExpressions.Regex.Match(lines[i], @"className=""([^""]+)""");
                        var funcMatch = System.Text.RegularExpressions.Regex.Match(lines[i], @"functionName=""([^""]+)""");

                        if (classMatch.Success && funcMatch.Success && classMatch.Groups[1].Value == className)
                        {
                            functions.Add(funcMatch.Groups[1].Value);
                        }
                    }
                }
            }

            return functions.Distinct().ToList();
        }

        public void SendDiagnostics(DocumentUri uri, List<Diagnostic> diagnostics)
        {
            _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
            {
                Uri = uri,
                Diagnostics = new Container<Diagnostic>(diagnostics)
            });
        }

        public IEnumerable<string> GetAvailableClasses()
        {
            if (_registry?.TargetLanguage == "csharp")
            {
                return _registry!.GetAvailableCsClasses();
            }
            else
            {
                return _registry!.GetAvailableJsClasses();
            }
        }

        public IEnumerable<MarathonTranspiler.Model.MethodInfo> GetMethodsForClass(string className)
        {
            if (_registry?.TargetLanguage == "csharp")
            {
                return _registry!.GetMethodsForCsClass(className);
            }
            else
            {
                return _registry!.GetMethodsForJsClass(className);
            }
        }

        public void ForceCompilation(DocumentUri uri)
        {
            // Cancel any pending compilation
            if (_compilationTokenSources.TryGetValue(uri, out var tokenSource))
            {
                tokenSource.Cancel();
                _compilationTokenSources.TryRemove(uri, out _);
            }

            // Trigger immediate compilation
            TriggerCompilation(uri);
        }

        public string GetTargetLanguageFromConfig(DocumentUri documentUri)
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
    }
}
