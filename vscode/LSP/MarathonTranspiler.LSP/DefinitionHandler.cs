using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace MarathonTranspiler.LSP
{
    public class DefinitionHandler : DefinitionHandlerBase
    {
        private readonly Workspace _workspace;

        public DefinitionHandler(Workspace workspace)
        {
            _workspace = workspace;
        }

        public override Task<LocationOrLocationLinks> Handle(DefinitionParams request, CancellationToken cancellationToken)
        {
            var uri = request.TextDocument.Uri;
            var position = request.Position;
            var lines = _workspace.GetDocumentLines(uri);

            if (lines == null || position.Line >= lines.Length)
                return Task.FromResult(new LocationOrLocationLinks());

            var line = lines[position.Line];

            // Check if we're in an annotation line
            if (line.TrimStart().StartsWith("@"))
            {
                // Extract id reference from the line if it exists
                var idMatch = Regex.Match(line, @"id=""([^""]+)""");
                if (idMatch.Success)
                {
                    // Get the id value
                    var idValue = idMatch.Groups[1].Value;
                    var idStartPos = line.IndexOf(idValue, StringComparison.Ordinal);

                    // Check if cursor is on this id
                    if (position.Character >= idStartPos && position.Character < idStartPos + idValue.Length)
                    {
                        // For @more annotation, find the original @run with this id
                        if (line.TrimStart().StartsWith("@more"))
                        {
                            var locations = new List<LocationOrLocationLink>();

                            // Search through all lines to find matching @run with the same id
                            for (int i = 0; i < lines.Length; i++)
                            {
                                if (lines[i].TrimStart().StartsWith("@run"))
                                {
                                    var runIdMatch = Regex.Match(lines[i], @"id=""([^""]+)""");
                                    if (runIdMatch.Success && runIdMatch.Groups[1].Value == idValue)
                                    {
                                        // Found the matching @run, create a location link
                                        var runIdStartPos = lines[i].IndexOf(idValue, StringComparison.Ordinal);
                                        locations.Add(
                                            new LocationOrLocationLink(
                                                new LocationLink
                                                {
                                                    OriginSelectionRange = new Range(
                                                        new Position(position.Line, idStartPos),
                                                        new Position(position.Line, idStartPos + idValue.Length)),
                                                    TargetUri = uri,
                                                    TargetRange = new Range(
                                                        new Position(i, 0),
                                                        new Position(i, lines[i].Length)),
                                                    TargetSelectionRange = new Range(
                                                        new Position(i, runIdStartPos),
                                                        new Position(i, runIdStartPos + idValue.Length))
                                                }));
                                    }
                                }
                            }

                            if (locations.Any())
                                return Task.FromResult(new LocationOrLocationLinks(locations));
                        }
                    }
                }

                // Check if we're on a class name reference
                var classNameMatch = Regex.Match(line, @"className=""([^""]+)""");
                if (classNameMatch.Success)
                {
                    var className = classNameMatch.Groups[1].Value;
                    var classNameStartPos = line.IndexOf(className, StringComparison.Ordinal);

                    // Check if cursor is on this class name
                    if (position.Character >= classNameStartPos && position.Character < classNameStartPos + className.Length)
                    {
                        var locations = new List<LocationOrLocationLink>();

                        // Find the first @varInit for this class (class declaration)
                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (lines[i].TrimStart().StartsWith("@varInit"))
                            {
                                var varInitClassMatch = Regex.Match(lines[i], @"className=""([^""]+)""");
                                if (varInitClassMatch.Success && varInitClassMatch.Groups[1].Value == className)
                                {
                                    var targetClassNameStart = lines[i].IndexOf(className, StringComparison.Ordinal);
                                    locations.Add(
                                        new LocationOrLocationLink(
                                            new LocationLink
                                            {
                                                OriginSelectionRange = new Range(
                                                    new Position(position.Line, classNameStartPos),
                                                    new Position(position.Line, classNameStartPos + className.Length)),
                                                TargetUri = uri,
                                                TargetRange = new Range(
                                                    new Position(i, 0),
                                                    new Position(i, lines[i].Length)),
                                                TargetSelectionRange = new Range(
                                                    new Position(i, targetClassNameStart),
                                                    new Position(i, targetClassNameStart + className.Length))
                                            }));

                                    // Return on first match for class name (should be the declaration)
                                    return Task.FromResult(new LocationOrLocationLinks(locations));
                                }
                            }
                        }
                    }
                }

                // Check if we're on a function name reference
                var functionNameMatch = Regex.Match(line, @"functionName=""([^""]+)""");
                if (functionNameMatch.Success)
                {
                    var functionName = functionNameMatch.Groups[1].Value;
                    var functionNameStartPos = line.IndexOf(functionName, StringComparison.Ordinal);

                    // Check if cursor is on this function name
                    if (position.Character >= functionNameStartPos && position.Character < functionNameStartPos + functionName.Length)
                    {
                        // Get the class name from the current line
                        var curClassNameMatch = Regex.Match(line, @"className=""([^""]+)""");
                        if (curClassNameMatch.Success)
                        {
                            var curClassName = curClassNameMatch.Groups[1].Value;
                            var locations = new List<LocationOrLocationLink>();

                            // Find all function declarations (other @run blocks) with the same name and class
                            for (int i = 0; i < lines.Length; i++)
                            {
                                if (i != position.Line && lines[i].TrimStart().StartsWith("@run"))
                                {
                                    var otherClassNameMatch = Regex.Match(lines[i], @"className=""([^""]+)""");
                                    var otherFunctionNameMatch = Regex.Match(lines[i], @"functionName=""([^""]+)""");

                                    if (otherClassNameMatch.Success && otherFunctionNameMatch.Success &&
                                        otherClassNameMatch.Groups[1].Value == curClassName &&
                                        otherFunctionNameMatch.Groups[1].Value == functionName)
                                    {
                                        var targetFunctionNameStart = lines[i].IndexOf(functionName, StringComparison.Ordinal);
                                        locations.Add(
                                            new LocationOrLocationLink(
                                                new LocationLink
                                                {
                                                    OriginSelectionRange = new Range(
                                                        new Position(position.Line, functionNameStartPos),
                                                        new Position(position.Line, functionNameStartPos + functionName.Length)),
                                                    TargetUri = uri,
                                                    TargetRange = new Range(
                                                        new Position(i, 0),
                                                        new Position(i, lines[i].Length)),
                                                    TargetSelectionRange = new Range(
                                                        new Position(i, targetFunctionNameStart),
                                                        new Position(i, targetFunctionNameStart + functionName.Length))
                                                }));
                                    }
                                }
                            }

                            if (locations.Any())
                                return Task.FromResult(new LocationOrLocationLinks(locations));
                        }
                    }
                }
            }

            return Task.FromResult(new LocationOrLocationLinks());
        }

        protected override DefinitionRegistrationOptions CreateRegistrationOptions(DefinitionCapability capability, ClientCapabilities clientCapabilities)
        {
            return new DefinitionRegistrationOptions
            {
                DocumentSelector = new TextDocumentSelector(new TextDocumentFilter
                {
                    Pattern = "**/*.mrt",
                    Language = "mrt"
                })
            };
        }
    }
}
