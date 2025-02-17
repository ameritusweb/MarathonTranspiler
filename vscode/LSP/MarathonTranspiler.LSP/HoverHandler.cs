using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Threading;
using System.Threading.Tasks;

namespace MarathonTranspiler.LSP
{
    public class HoverHandler : HoverHandlerBase
    {
        private readonly Workspace _workspace;

        public HoverHandler(Workspace workspace)
        {
            _workspace = workspace;
        }

        // 🔹 Annotation documentation dictionary
        private static readonly Dictionary<string, string> AnnotationDocs = new()
        {
            { "@varInit", "**@varInit** - Initializes a variable.\n\nParameters:\n- `className` → The class the variable belongs to.\n- `type` → The type of the variable.\n\nExample:\n```mrt\n@varInit(className=\"Parser\", type=\"string\")\nthis.Text = \"\";\n```" },
            { "@run", "**@run** - Executes a function in the specified class.\n\nParameters:\n- `id` → A unique identifier for the execution.\n- `className` → The class containing the function.\n- `functionName` → The function to execute.\n\nExample:\n```mrt\n@run(id=\"parse\", className=\"Parser\", functionName=\"Parse\")\n```" },
            { "@more", "**@more** - Adds more logic dynamically.\n\nParameters:\n- `id` → The identifier to attach additional logic to.\n\nExample:\n```mrt\n@more(id=\"parseLoop\")\n```" },
            { "@condition", "**@condition** - Defines a conditional execution block.\n\nParameters:\n- `expression` → The condition to check.\n\nExample:\n```mrt\n@condition(expression=\"this.Position < this.Text.Length\")\n```" }
        };

        public override Task<Hover?> Handle(HoverParams request, CancellationToken cancellationToken)
        {
            var hoveredWord = ExtractHoveredWord(request);
            if (hoveredWord == null || !AnnotationDocs.ContainsKey(hoveredWord))
            {
                return Task.FromResult<Hover?>(null); // No documentation found
            }

            return Task.FromResult(new Hover
            {
                Contents = new MarkedStringsOrMarkupContent(new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = AnnotationDocs[hoveredWord]
                })
            });
        }

        private string? ExtractHoveredWord(HoverParams request)
        {
            var lines = _workspace.GetDocumentLines(request.TextDocument.Uri);
            if (lines == null || request.Position.Line >= lines.Length)
            {
                return null;
            }

            var line = lines[request.Position.Line];
            var words = line.Split(' '); // Simplified: Should handle special cases

            // Find the annotation the user is hovering over
            return words.FirstOrDefault(word => word.StartsWith("@"));
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
