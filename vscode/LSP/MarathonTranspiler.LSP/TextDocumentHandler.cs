using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace MarathonTranspiler.LSP
{
    public class TextDocumentHandler : TextDocumentSyncHandlerBase
    {
        private readonly Workspace _workspace;

        private static readonly List<string> ValidAnnotations = new()
        {
            "@varInit", "@run", "@more", "@condition", "@parameter", "@assert", "@domInit", "@onEvent"
        };

        private static readonly Dictionary<string, List<string>> RequiredProperties = new()
        {
            { "@varInit", new List<string> { "className", "type" } },
            { "@run", new List<string> { "id", "className", "functionName" } },
            { "@more", new List<string> { "id" } },
            { "@condition", new List<string> { "expression" } },
            { "@assert", new List<string> { "condition" } },
            { "@parameter", new List<string> { "name", "type" } },
            { "@domInit", new List<string> { "id", "parent" } },
            { "@onEvent", new List<string> { "event", "target" } },
        };

        public TextDocumentHandler(Workspace workspace)
        {
            _workspace = workspace;
        }

        public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
        {
            _workspace.UpdateDocument(request.TextDocument.Uri, request.TextDocument.Text);
            RunDiagnostics(request.TextDocument.Uri);
            return Task.FromResult(Unit.Value);
        }

        public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
        {
            _workspace.UpdateDocument(request.TextDocument.Uri, request.ContentChanges.FirstOrDefault().Text);
            RunDiagnostics(request.TextDocument.Uri);
            return Task.FromResult(Unit.Value);
        }

        public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
        {
            _workspace.RemoveDocument(request.TextDocument.Uri);
            return Task.FromResult(Unit.Value);
        }

        private void RunDiagnostics(DocumentUri uri)
        {
            var diagnostics = new List<Diagnostic>();
            var lines = _workspace.GetDocumentLines(uri);
            if (lines == null) return;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                if (line.StartsWith("@"))
                {
                    var match = Regex.Match(line, @"^@(\w+)(\((.*?)\))?$");
                    if (match.Success)
                    {
                        var annotation = "@" + match.Groups[1].Value;
                        var properties = match.Groups[3].Value;

                        // 1️⃣ **Check if annotation is valid**
                        if (!ValidAnnotations.Contains(annotation))
                        {
                            diagnostics.Add(new Diagnostic
                            {
                                Range = new Range(new Position(i, 0), new Position(i, line.Length)),
                                Message = $"Unknown annotation: `{annotation}`",
                                Severity = DiagnosticSeverity.Error
                            });
                        }
                        else
                        {
                            // 2️⃣ **Check for required properties**
                            var missingProperties = RequiredProperties[annotation]
                                .Where(p => !properties.Contains(p + "="))
                                .ToList();

                            if (missingProperties.Any())
                            {
                                diagnostics.Add(new Diagnostic
                                {
                                    Range = new Range(new Position(i, 0), new Position(i, line.Length)),
                                    Message = $"Missing required properties: {string.Join(", ", missingProperties)}",
                                    Severity = DiagnosticSeverity.Warning
                                });
                            }

                            // 3️⃣ **Check for invalid properties**
                            var propertyMatches = Regex.Matches(properties, @"(\w+)=");
                            foreach (Match prop in propertyMatches)
                            {
                                var propertyName = prop.Groups[1].Value;
                                if (!RequiredProperties[annotation].Contains(propertyName))
                                {
                                    diagnostics.Add(new Diagnostic
                                    {
                                        Range = new Range(new Position(i, properties.IndexOf(propertyName)),
                                                          new Position(i, properties.IndexOf(propertyName) + propertyName.Length)),
                                        Message = $"Invalid property `{propertyName}` for `{annotation}`",
                                        Severity = DiagnosticSeverity.Warning
                                    });
                                }
                            }
                        }
                    }
                    else
                    {
                        // 4️⃣ **Malformed annotation error**
                        diagnostics.Add(new Diagnostic
                        {
                            Range = new Range(new Position(i, 0), new Position(i, line.Length)),
                            Message = $"Invalid annotation syntax: `{line}`",
                            Severity = DiagnosticSeverity.Error
                        });
                    }
                }
            }

            // 🔹 **Send diagnostics via the workspace**
            _workspace.SendDiagnostics(uri, diagnostics);
        }

        protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
        {
            return new TextDocumentSyncRegistrationOptions
            {
                DocumentSelector = new TextDocumentSelector(new TextDocumentFilter
                {
                    Pattern = "**/*.mrt",
                    Language = "mrt"
                }),
                Change = TextDocumentSyncKind.Full,
                Save = new SaveOptions() { IncludeText = true },
            };

            
        }

        public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
        {
            return new TextDocumentAttributes(uri, "mrt");
        }

        public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Unit.Value);
        }
    }
}
