using System;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using System.Collections.Concurrent;

namespace MarathonTranspiler.LSP
{
    public class Workspace
    {
        public string Root { get; private set; } = string.Empty;
        private ILanguageServer _server;

        // 🔹 Store open documents and their content
        private readonly ConcurrentDictionary<DocumentUri, string[]> _documents = new();

        public void Initialize(ILanguageServer server, string rootPath)
        {
            _server = server;
            Root = rootPath;
            _server.Log($"MRT Workspace initialized at: {Root}");
        }

        public void SendNotification(string message)
        {
            _server.Window.ShowMessage(new ShowMessageParams
            {
                Type = MessageType.Info,
                Message = message
            });
        }

        public void UpdateDocument(DocumentUri uri, string content)
        {
            _documents[uri] = content.Split('\n'); // Store document as lines
        }

        public void RemoveDocument(DocumentUri uri)
        {
            _documents.TryRemove(uri, out _);
            SendDiagnostics(uri, new List<Diagnostic>()); // Clear diagnostics when file closes
        }

        public string[]? GetDocumentLines(DocumentUri uri)
        {
            return _documents.TryGetValue(uri, out var lines) ? lines : null;
        }

        public void SendDiagnostics(DocumentUri uri, List<Diagnostic> diagnostics)
        {
            _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
            {
                Uri = uri,
                Diagnostics = new Container<Diagnostic>(diagnostics)
            });
        }
    }
}
