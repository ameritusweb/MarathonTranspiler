using System;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using System.Collections.Concurrent;
using MarathonTranspiler.LSP.Extensions;

namespace MarathonTranspiler.LSP
{
    public class Workspace
    {
        private readonly ConcurrentDictionary<DocumentUri, string> _documents = new();
        private readonly ConcurrentDictionary<DocumentUri, string[]> _documentLines = new();
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
                var fileInfo = new FileInfo(Path.Combine(uri.ToUri().AbsolutePath, "lib"));
                _registry.Initialize(fileInfo.DirectoryName!);
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
            return _registry!.GetAvailableClasses();
        }

        public IEnumerable<string> GetMethodsForClass(string className)
        {
            return _registry!.GetMethodsForClass(className);
        }
    }
}
