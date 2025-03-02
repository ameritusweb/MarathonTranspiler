using System;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using System.Collections.Concurrent;
using MarathonTranspiler.LSP.Extensions;
using System.Reflection;

namespace MarathonTranspiler.LSP
{
    public class Workspace
    {
        private readonly ConcurrentDictionary<DocumentUri, string> _documents = new();
        private readonly ConcurrentDictionary<DocumentUri, string[]> _documentLines = new();
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

            _documents[uri] = text;
            _documentLines[uri] = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
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

        public IEnumerable<Model.MethodInfo> GetMethodsForClass(string className)
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
