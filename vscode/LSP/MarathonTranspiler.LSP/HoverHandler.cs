using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace MarathonTranspiler.LSP
{
    public class HoverHandler : HoverHandlerBase
    {
        private readonly Workspace _workspace;

        private static readonly Dictionary<string, string> AnnotationDocumentation = new()
        {
            { "@varInit", "Initializes a variable with the specified class and type.\n\n**Required properties:**\n- `className`: The class where this variable is defined\n- `type`: The data type of the variable\n\n**When used inside a @run block:**\n- Can define nested classes with properties using array syntax: `type=[ { name=\"PropName\", type=\"string\" }, ... ]`" },
            { "@run", "Executes code within the context of a specific class and function.\n\n**Required properties:**\n- `id`: Unique identifier for this execution block\n- `className`: The class where this function is defined\n- `functionName`: The function to execute" },
            { "@more", "Adds additional code to an existing execution block.\n\n**Required properties:**\n- `id`: The identifier of the execution block to extend" },
            { "@condition", "Defines a conditional expression.\n\n**Required properties:**\n- `expression`: The boolean expression to evaluate" }
        };

        private static readonly Dictionary<string, Dictionary<string, string>> PropertyDocumentation = new()
        {
            { "@varInit", new Dictionary<string, string>
                {
                    { "className", "The class where this variable is defined" },
                    { "type", "The data type of the variable (e.g., string, int, bool)" }
                }
            },
            { "@run", new Dictionary<string, string>
                {
                    { "id", "Unique identifier for this execution block" },
                    { "className", "The class where this function is defined" },
                    { "functionName", "The function to execute" }
                }
            },
            { "@more", new Dictionary<string, string>
                {
                    { "id", "The identifier of the execution block to extend" }
                }
            },
            { "@condition", new Dictionary<string, string>
                {
                    { "expression", "The boolean expression to evaluate" }
                }
            }
        };

        public HoverHandler(Workspace workspace)
        {
            _workspace = workspace;
        }

        public override Task<Hover> Handle(HoverParams request, CancellationToken cancellationToken)
        {
            var uri = request.TextDocument.Uri;
            var position = request.Position;
            var lines = _workspace.GetDocumentLines(uri);

            if (lines == null || position.Line >= lines.Length)
                return Task.FromResult<Hover>(null);

            var line = lines[position.Line];

            // Check if hovering over an annotation
            if (line.TrimStart().StartsWith("@"))
            {
                var match = Regex.Match(line, @"^(\s*)(@\w+)(\(.*\))?$");
                if (match.Success)
                {
                    var annotation = match.Groups[2].Value;
                    var indentLength = match.Groups[1].Value.Length;

                    // If cursor is within the annotation keyword
                    if (position.Character >= indentLength && position.Character < indentLength + annotation.Length)
                    {
                        if (AnnotationDocumentation.TryGetValue(annotation, out var documentation))
                        {
                            return Task.FromResult(new Hover
                            {
                                Contents = new MarkedStringsOrMarkupContent(
                                    new MarkupContent
                                    {
                                        Kind = MarkupKind.Markdown,
                                        Value = $"# {annotation}\n\n{documentation}"
                                    }),
                                Range = new Range(
                                    new Position(position.Line, indentLength),
                                    new Position(position.Line, indentLength + annotation.Length))
                            });
                        }
                    }

                    // Check if hovering over a property
                    if (match.Groups[3].Success)
                    {
                        var propertiesText = match.Groups[3].Value;
                        var propertyMatches = Regex.Matches(propertiesText, @"(\w+)=(""[^""]*""|[^,\)]+)");

                        foreach (Match propertyMatch in propertyMatches)
                        {
                            var propertyName = propertyMatch.Groups[1].Value;
                            var startPos = line.IndexOf(propertyName, indentLength + annotation.Length);

                            if (position.Character >= startPos && position.Character < startPos + propertyName.Length)
                            {
                                if (PropertyDocumentation.TryGetValue(annotation, out var propertyDocs) &&
                                    propertyDocs.TryGetValue(propertyName, out var propertyDoc))
                                {
                                    return Task.FromResult(new Hover
                                    {
                                        Contents = new MarkedStringsOrMarkupContent(
                                            new MarkupContent
                                            {
                                                Kind = MarkupKind.Markdown,
                                                Value = $"## {propertyName}\n\n{propertyDoc}"
                                            }),
                                        Range = new Range(
                                            new Position(position.Line, startPos),
                                            new Position(position.Line, startPos + propertyName.Length))
                                    });
                                }
                            }
                        }
                    }
                }
            }

            return Task.FromResult<Hover>(null);
        }

        protected override HoverRegistrationOptions CreateRegistrationOptions(HoverCapability capability, ClientCapabilities clientCapabilities)
        {
            return new HoverRegistrationOptions
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
