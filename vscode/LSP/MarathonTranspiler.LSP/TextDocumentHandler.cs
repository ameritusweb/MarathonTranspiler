using MarathonTranspiler.LSP.Model;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using System;
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
            "@varInit", "@run", "@more", "@condition", "@parameter", "@assert", "@domInit", "@onEvent", "@xml"
        };

        private static readonly Dictionary<string, List<string>> RequiredProperties = new()
        {
            { "@varInit", new List<string> { "className", "type" } },
            { "@run", new List<string> { "className", "functionName" } },
            { "@more", new List<string> { "id" } },
            { "@condition", new List<string> { "expression" } },
            { "@assert", new List<string> { "className", "condition" } },
            { "@parameter", new List<string> { "name", "type" } },
            { "@domInit", new List<string> { "target", "tag" } },
            { "@onEvent", new List<string> { "className", "event", "target" } },
            { "@xml", new List<string> { } },
        };

        private static readonly Dictionary<string, List<string>> OptionalProperties = new()
        {
            { "@parameter", new List<string> { "value" } },
            { "@domInit", new List<string> { "class" } },
            { "@xml", new List<string> { "pageName", "componentName" } },
            { "@run", new List<string> { "id" } },
        };

        private int _lineCount = 0;

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
            var uri = request.TextDocument.Uri;
            _workspace.UpdateDocument(uri, request.ContentChanges.FirstOrDefault().Text);
            string[] lines = _workspace.GetDocumentLines(uri);
            if (lines.Length != _lineCount)
            {
                RunDiagnostics(request.TextDocument.Uri);
            }

            this._lineCount = lines.Length;
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

            bool insideRunBlock = false;
            string currentClassName = "";

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

                        // Track when we enter/exit a run block
                        if (annotation == "@run")
                        {
                            insideRunBlock = true;

                            // Extract className for context
                            var classNameMatch = Regex.Match(properties, @"className=""([^""]+)""");
                            if (classNameMatch.Success)
                            {
                                currentClassName = classNameMatch.Groups[1].Value;
                            }
                        }
                        else if (insideRunBlock && line.Contains("var ") && !line.Contains("@"))
                        {
                            // We've reached code inside the run block
                            insideRunBlock = false;
                        }

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
                        // Special handling for nested varInit inside run blocks
                        else if (annotation == "@varInit" && insideRunBlock)
                        {
                            // Check if this is a type definition with array syntax
                            var typeMatch = Regex.Match(properties, @"type=\[(.*?)\]");
                            if (typeMatch.Success)
                            {
                                var typeContent = typeMatch.Groups[1].Value;

                                // Validate that the type content has valid property definitions
                                if (!ValidatePropertyDefinitions(typeContent))
                                {
                                    diagnostics.Add(new Diagnostic
                                    {
                                        Range = new Range(new Position(i, properties.IndexOf("type=")),
                                                        new Position(i, properties.IndexOf("type=") + 5 + typeContent.Length + 2)),
                                        Message = "Invalid property definition array. Format should be: [ { name=\"PropName\", type=\"PropType\" }, ... ]",
                                        Severity = DiagnosticSeverity.Error
                                    });
                                }

                                // Also check that className is different from the run block's className
                                var classNameMatch = Regex.Match(properties, @"className=""([^""]+)""");
                                if (classNameMatch.Success && classNameMatch.Groups[1].Value == currentClassName)
                                {
                                    diagnostics.Add(new Diagnostic
                                    {
                                        Range = new Range(new Position(i, properties.IndexOf("className=")),
                                                         new Position(i, properties.IndexOf("className=") + 10 + currentClassName.Length + 2)),
                                        Message = "Nested class definition should have a different class name than the containing method",
                                        Severity = DiagnosticSeverity.Warning
                                    });
                                }
                            }

                            // For regular varInit checks, still validate required properties
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
                        }
                        else
                        {
                            // Standard property validation for other annotation types
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
                                if (!RequiredProperties[annotation].Contains(propertyName) && !this.IsOptional(annotation, propertyName))
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

        private bool ValidatePropertyDefinitions(string content)
        {
            // Simple regex check for the expected pattern
            var pattern = @"\{\s*name\s*=\s*""[^""]+""s*,\s*type\s*=\s*""[^""]+""s*\}";
            var matches = Regex.Matches(content, pattern);

            // Allow a more lenient check by just ensuring we have at least one property with name and type
            return content.Contains("name=") && content.Contains("type=");
        }

        private bool IsOptional(string annotation, string property)
        {
            return OptionalProperties.ContainsKey(annotation) && OptionalProperties[annotation].Contains(property);
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
