using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MarathonTranspiler.LSP
{
    public class CompletionHandler : CompletionHandlerBase
    {
        // 🔹 Annotation keyword suggestions
        private static readonly CompletionList AnnotationCompletions = new(
            new List<CompletionItem>
            {
                new CompletionItem { Label = "@varInit", Kind = CompletionItemKind.Keyword, InsertText = "@varInit(" },
                new CompletionItem { Label = "@run", Kind = CompletionItemKind.Keyword, InsertText = "@run(" },
                new CompletionItem { Label = "@more", Kind = CompletionItemKind.Keyword, InsertText = "@more(" },
                new CompletionItem { Label = "@condition", Kind = CompletionItemKind.Keyword, InsertText = "@condition(" }
            });

        // 🔹 Parameter suggestions for each annotation
        private static readonly Dictionary<string, List<CompletionItem>> AnnotationParameters = new()
        {
            { "@varInit", new List<CompletionItem>
                {
                    new CompletionItem { Label = "className", Kind = CompletionItemKind.Property, InsertText = "className=\"" },
                    new CompletionItem { Label = "type", Kind = CompletionItemKind.Property, InsertText = "type=\"" }
                }
            },
            { "@run", new List<CompletionItem>
                {
                    new CompletionItem { Label = "id", Kind = CompletionItemKind.Property, InsertText = "id=\"" },
                    new CompletionItem { Label = "className", Kind = CompletionItemKind.Property, InsertText = "className=\"" },
                    new CompletionItem { Label = "functionName", Kind = CompletionItemKind.Property, InsertText = "functionName=\"" }
                }
            },
            { "@more", new List<CompletionItem>
                {
                    new CompletionItem { Label = "id", Kind = CompletionItemKind.Property, InsertText = "id=\"" }
                }
            },
            { "@condition", new List<CompletionItem>
                {
                    new CompletionItem { Label = "expression", Kind = CompletionItemKind.Property, InsertText = "expression=\"" }
                }
            }
        };

        // 🔹 Value suggestions for certain parameters
        private static readonly Dictionary<string, List<CompletionItem>> ParameterValueSuggestions = new()
        {
            { "type", new List<CompletionItem>
                {
                    new CompletionItem { Label = "string", Kind = CompletionItemKind.Value, InsertText = "string\"" },
                    new CompletionItem { Label = "int", Kind = CompletionItemKind.Value, InsertText = "int\"" },
                    new CompletionItem { Label = "bool", Kind = CompletionItemKind.Value, InsertText = "bool\"" }
                }
            },
            { "functionName", new List<CompletionItem>
                {
                    new CompletionItem { Label = "Parse", Kind = CompletionItemKind.Function, InsertText = "Parse\"" },
                    new CompletionItem { Label = "Execute", Kind = CompletionItemKind.Function, InsertText = "Execute\"" }
                }
            }
        };

        public override Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            var text = request.TextDocument.Uri.ToString();  // Get document URI
            var position = request.Position;  // Get cursor position

            // 🔹 If user is typing `@`, suggest annotations
            if (position.Character == 0 || text[position.Character - 1] == '@')
            {
                return Task.FromResult(AnnotationCompletions);
            }

            // 🔹 If inside an annotation (e.g., `@varInit(`), suggest parameters
            var lineText = text[..position.Character]; // Get text up to the cursor
            var annotation = AnnotationParameters.Keys.FirstOrDefault(a => lineText.Contains(a));

            if (annotation != null && lineText.EndsWith("("))
            {
                return Task.FromResult(new CompletionList(AnnotationParameters[annotation]));
            }

            // 🔹 If typing after `=`, suggest values for known parameters
            var lastWord = lineText.Split(' ').Last();
            if (lastWord.Contains("="))
            {
                var parameterName = lastWord.Split('=').First();
                if (ParameterValueSuggestions.ContainsKey(parameterName))
                {
                    return Task.FromResult(new CompletionList(ParameterValueSuggestions[parameterName]));
                }
            }

            return Task.FromResult(new CompletionList());
        }

        public override Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken)
        {
            return Task.FromResult(request);
        }

        protected override CompletionRegistrationOptions CreateRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities)
        {
            return new CompletionRegistrationOptions
            {
                TriggerCharacters = new Container<string>(new[] { "@", "(", "=" }), // Trigger on `@`, `(`, `=`
                DocumentSelector = new TextDocumentSelector(new TextDocumentFilter
                {
                    Pattern = "**/*.mrt",
                    Language = "mrt"
                })
            };
        }
    }
}
