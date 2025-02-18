using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace MarathonTranspiler.LSP
{
    public class RenameHandler : RenameHandlerBase
    {
        private readonly Workspace _workspace;

        public RenameHandler(Workspace workspace)
        {
            _workspace = workspace;
        }

        public override Task<WorkspaceEdit> Handle(RenameParams request, CancellationToken cancellationToken)
        {
            var uri = request.TextDocument.Uri;
            var position = request.Position;
            var newName = request.NewName;
            var lines = _workspace.GetDocumentLines(uri);

            if (lines == null || position.Line >= lines.Length)
                return Task.FromResult(new WorkspaceEdit());

            var line = lines[position.Line];

            if (!line.TrimStart().StartsWith("@"))
                return Task.FromResult(new WorkspaceEdit());

            var edits = new Dictionary<DocumentUri, IEnumerable<TextEdit>>();

            // Detect what is being renamed: ID, class name, or function name
            string renameType = null;
            string oldName = null;

            // Check if hovering over an id
            var idMatch = Regex.Match(line, @"id=""([^""]+)""");
            if (idMatch.Success)
            {
                var idValue = idMatch.Groups[1].Value;
                var idStartPos = line.IndexOf(idValue, StringComparison.Ordinal);

                if (position.Character >= idStartPos && position.Character < idStartPos + idValue.Length)
                {
                    renameType = "id";
                    oldName = idValue;
                }
            }

            // Check if hovering over a class name
            if (renameType == null)
            {
                var classNameMatch = Regex.Match(line, @"className=""([^""]+)""");
                if (classNameMatch.Success)
                {
                    var className = classNameMatch.Groups[1].Value;
                    var classNameStartPos = line.IndexOf(className, StringComparison.Ordinal);

                    if (position.Character >= classNameStartPos && position.Character < classNameStartPos + className.Length)
                    {
                        renameType = "className";
                        oldName = className;
                    }
                }
            }

            // Check if hovering over a function name
            if (renameType == null)
            {
                var functionNameMatch = Regex.Match(line, @"functionName=""([^""]+)""");
                if (functionNameMatch.Success)
                {
                    var functionName = functionNameMatch.Groups[1].Value;
                    var functionNameStartPos = line.IndexOf(functionName, StringComparison.Ordinal);

                    if (position.Character >= functionNameStartPos && position.Character < functionNameStartPos + functionName.Length)
                    {
                        renameType = "functionName";
                        oldName = functionName;

                        // For function names, we need to consider the class context
                        var classContext = Regex.Match(line, @"className=""([^""]+)""");
                        if (classContext.Success)
                        {
                            oldName = $"{classContext.Groups[1].Value}.{functionName}";
                        }
                    }
                }
            }

            if (renameType == null || oldName == null)
                return Task.FromResult(new WorkspaceEdit());

            // Process each document for changes
            foreach (var documentUri in _workspace.GetDocumentUris())
            {
                var documentEdits = new List<TextEdit>();
                var documentLines = _workspace.GetDocumentLines(documentUri);

                if (documentLines == null)
                    continue;

                for (int i = 0; i < documentLines.Length; i++)
                {
                    var currentLine = documentLines[i];

                    if (!currentLine.TrimStart().StartsWith("@"))
                        continue;

                    switch (renameType)
                    {
                        case "id":
                            HandleIdRename(currentLine, i, oldName, newName, documentEdits);
                            break;

                        case "className":
                            HandleClassNameRename(currentLine, i, oldName, newName, documentEdits);
                            break;

                        case "functionName":
                            HandleFunctionNameRename(currentLine, i, oldName, newName, documentEdits);
                            break;
                    }
                }

                if (documentEdits.Count > 0)
                {
                    edits[documentUri] = documentEdits;
                }
            }

            return Task.FromResult(new WorkspaceEdit
            {
                Changes = edits
            });
        }

        private void HandleIdRename(string line, int lineNumber, string oldId, string newId, List<TextEdit> edits)
        {
            var idMatches = Regex.Matches(line, @"id=""([^""]+)""");
            foreach (Match match in idMatches)
            {
                var idValue = match.Groups[1].Value;
                if (idValue == oldId)
                {
                    var idStartPos = match.Groups[1].Index;
                    edits.Add(new TextEdit
                    {
                        Range = new Range(
                            new Position(lineNumber, idStartPos),
                            new Position(lineNumber, idStartPos + oldId.Length)),
                        NewText = newId
                    });
                }
            }
        }

        private void HandleClassNameRename(string line, int lineNumber, string oldClassName, string newClassName, List<TextEdit> edits)
        {
            var classNameMatches = Regex.Matches(line, @"className=""([^""]+)""");
            foreach (Match match in classNameMatches)
            {
                var className = match.Groups[1].Value;
                if (className == oldClassName)
                {
                    var classNameStartPos = match.Groups[1].Index;
                    edits.Add(new TextEdit
                    {
                        Range = new Range(
                            new Position(lineNumber, classNameStartPos),
                            new Position(lineNumber, classNameStartPos + oldClassName.Length)),
                        NewText = newClassName
                    });
                }
            }
        }

        private void HandleFunctionNameRename(string line, int lineNumber, string oldFullName, string newName, List<TextEdit> edits)
        {
            var parts = oldFullName.Split('.');
            if (parts.Length != 2)
                return;

            var oldClassName = parts[0];
            var oldFunctionName = parts[1];

            // Only rename if both class and function name match
            var classNameMatch = Regex.Match(line, @"className=""([^""]+)""");
            var functionNameMatch = Regex.Match(line, @"functionName=""([^""]+)""");

            if (classNameMatch.Success && functionNameMatch.Success &&
                classNameMatch.Groups[1].Value == oldClassName &&
                functionNameMatch.Groups[1].Value == oldFunctionName)
            {
                var functionNameStartPos = functionNameMatch.Groups[1].Index;
                edits.Add(new TextEdit
                {
                    Range = new Range(
                        new Position(lineNumber, functionNameStartPos),
                        new Position(lineNumber, functionNameStartPos + oldFunctionName.Length)),
                    NewText = newName
                });
            }
        }

        protected override RenameRegistrationOptions CreateRegistrationOptions(RenameCapability capability, ClientCapabilities clientCapabilities)
        {
            return new RenameRegistrationOptions
            {
                DocumentSelector = new TextDocumentSelector(new TextDocumentFilter
                {
                    Pattern = "**/*.mrt",
                    Language = "mrt"
                }),
                PrepareProvider = true
            };
        }
    }
}