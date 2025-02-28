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
        private readonly Workspace _workspace;

        private static readonly List<CompletionItem> StandardSnippets = new()
        {
            new CompletionItem
            {
                Label = "@varInit",
                Kind = CompletionItemKind.Snippet,
                InsertTextFormat = InsertTextFormat.Snippet,
                // Remove @ from InsertText to prevent duplication
                InsertText = "varInit(className=\"${1:ClassName}\", type=\"${2:string}\")\nthis.${3:Variable} = ${4:Value};",
                Documentation = new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = "Initialize a variable with class name and type."
                }
            },
            new CompletionItem
            {
                Label = "@domInit",
                Kind = CompletionItemKind.Snippet,
                InsertTextFormat = InsertTextFormat.Snippet,
                // Remove @ from InsertText to prevent duplication
                InsertText = "domInit(target=\"${1:Target}\", tag=\"${2:Tag}\", class=\"${2:Class}\")\n",
                Documentation = new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = "Initialize a DOM element from an XML structure."
                }
            },
            new CompletionItem
            {
                Label = "@run",
                Kind = CompletionItemKind.Snippet,
                InsertTextFormat = InsertTextFormat.Snippet,
                // Remove @ from InsertText to prevent duplication
                InsertText = "run(id=\"${1:runId}\", className=\"${2:ClassName}\", functionName=\"${3:functionName}\")\n${4:// Code to execute}",
                Documentation = new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = "Define an execution block with a unique ID."
                }
            },
            new CompletionItem
            {
                Label = "@onEvent",
                Kind = CompletionItemKind.Snippet,
                InsertTextFormat = InsertTextFormat.Snippet,
                // Remove @ from InsertText to prevent duplication
                InsertText = "onEvent(className=\"${1:ClassName}\", event=\"${2:Event}\", target=\"${3:Target}\")\n${4:// Code to execute in response}",
                Documentation = new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = "Define an event handler."
                }
            },
            new CompletionItem
            {
                Label = "@condition",
                Kind = CompletionItemKind.Snippet,
                InsertTextFormat = InsertTextFormat.Snippet,
                // Remove @ from InsertText to prevent duplication
                InsertText = "condition(expression=\"${1:this.Position < this.Text.Length}\")",
                Documentation = new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = "Add a conditional expression."
                }
            },
            new CompletionItem
            {
                Label = "@assert",
                Kind = CompletionItemKind.Snippet,
                InsertTextFormat = InsertTextFormat.Snippet,
                // Remove @ from InsertText to prevent duplication
                InsertText = "assert(className=\"${1:ClassName}\", condition=\"${2:this.Position < this.Text.Length}\")",
                Documentation = new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = "Add an assert for a test case."
                }
            },
            new CompletionItem
            {
                Label = "@parameter",
                Kind = CompletionItemKind.Snippet,
                InsertTextFormat = InsertTextFormat.Snippet,
                // Remove @ from InsertText to prevent duplication
                InsertText = "parameter(name=\"${1:Name}\", type=\"${2:string}\")",
                Documentation = new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = "Declare a parameter to the function."
                }
            },
            new CompletionItem
            {
                Label = "@varInit (nested class)",
                Kind = CompletionItemKind.Snippet,
                InsertTextFormat = InsertTextFormat.Snippet,
                InsertText = "varInit(className=\"${1:ClassName}\", type=[ { name=\"${2:PropName}\", type=\"${3:string}\" }, { name=\"${4:PropName2}\", type=\"${5:string}\" } ])",
                Documentation = new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = "Define a nested class with properties inside a method."
                }
            }
        };

        // Parameter snippets remain unchanged
        private static readonly Dictionary<string, List<CompletionItem>> ParameterSnippets = new()
        {
            { "className", new List<CompletionItem>
                {
                    new CompletionItem {
                        Label = "className",
                        Kind = CompletionItemKind.Property,
                        InsertTextFormat = InsertTextFormat.Snippet,
                        InsertText = "className=\"${1:ClassName}\""
                    }
                }
            },
            { "pageName", new List<CompletionItem>
                {
                    new CompletionItem {
                        Label = "pageName",
                        Kind = CompletionItemKind.Property,
                        InsertTextFormat = InsertTextFormat.Snippet,
                        InsertText = "pageName=\"${1:PageName}\""
                    }
                }
            },
            { "componentName", new List<CompletionItem>
                {
                    new CompletionItem {
                        Label = "componentName",
                        Kind = CompletionItemKind.Property,
                        InsertTextFormat = InsertTextFormat.Snippet,
                        InsertText = "componentName=\"${1:ComponentName}\""
                    }
                }
            },
            { "name", new List<CompletionItem>
                {
                    new CompletionItem {
                        Label = "name",
                        Kind = CompletionItemKind.Property,
                        InsertTextFormat = InsertTextFormat.Snippet,
                        InsertText = "name=\"${1:Name}\""
                    }
                }
            },
            { "type", new List<CompletionItem>
                {
                    new CompletionItem {
                        Label = "type",
                        Kind = CompletionItemKind.Property,
                        InsertTextFormat = InsertTextFormat.Snippet,
                        InsertText = "type=\"${1|string,int,bool,double,object|}\""
                    }
                }
            },
            { "id", new List<CompletionItem>
                {
                    new CompletionItem {
                        Label = "id",
                        Kind = CompletionItemKind.Property,
                        InsertTextFormat = InsertTextFormat.Snippet,
                        InsertText = "id=\"${1:uniqueId}\""
                    }
                }
            },
            { "parent", new List<CompletionItem>
                {
                    new CompletionItem {
                        Label = "parent",
                        Kind = CompletionItemKind.Property,
                        InsertTextFormat = InsertTextFormat.Snippet,
                        InsertText = "parent=\"${1:parent}\""
                    }
                }
            },
            { "functionName", new List<CompletionItem>
                {
                    new CompletionItem {
                        Label = "functionName",
                        Kind = CompletionItemKind.Property,
                        InsertTextFormat = InsertTextFormat.Snippet,
                        InsertText = "functionName=\"${1:functionName}\""
                    }
                }
            },
            { "expression", new List<CompletionItem>
                {
                    new CompletionItem {
                        Label = "expression",
                        Kind = CompletionItemKind.Property,
                        InsertTextFormat = InsertTextFormat.Snippet,
                        InsertText = "expression=\"${1:condition}\""
                    }
                }
            },
            { "event", new List<CompletionItem>
                {
                    new CompletionItem {
                        Label = "event",
                        Kind = CompletionItemKind.Property,
                        InsertTextFormat = InsertTextFormat.Snippet,
                        InsertText = "event=\"${1:Name}\""
                    }
                }
            },
            { "target", new List<CompletionItem>
                {
                    new CompletionItem {
                        Label = "target",
                        Kind = CompletionItemKind.Property,
                        InsertTextFormat = InsertTextFormat.Snippet,
                        InsertText = "target=\"${1:Target}\""
                    }
                }
            },
            { "tag", new List<CompletionItem>
                {
                    new CompletionItem {
                        Label = "tag",
                        Kind = CompletionItemKind.Property,
                        InsertTextFormat = InsertTextFormat.Snippet,
                        InsertText = "tag=\"${1:Tag}\""
                    }
                }
            },
            { "class", new List<CompletionItem>
                {
                    new CompletionItem {
                        Label = "class",
                        Kind = CompletionItemKind.Property,
                        InsertTextFormat = InsertTextFormat.Snippet,
                        InsertText = "class=\"${1:Class}\""
                    }
                }
            },
            { "condition", new List<CompletionItem>
                {
                    new CompletionItem {
                        Label = "condition",
                        Kind = CompletionItemKind.Property,
                        InsertTextFormat = InsertTextFormat.Snippet,
                        InsertText = "condition=\"${1:expression}\""
                    }
                }
            }
        };

        public CompletionHandler(Workspace workspace)
        {
            _workspace = workspace;
        }

        public override Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            var uri = request.TextDocument.Uri;
            var position = request.Position;

            // Get the document text and lines
            var lines = _workspace.GetDocumentLines(uri);
            if (lines == null || position.Line >= lines.Length)
                return Task.FromResult(new CompletionList());

            var line = lines[position.Line];
            if (string.IsNullOrEmpty(line) || position.Character > line.Length)
                return Task.FromResult(new CompletionList());

            var linePrefix = position.Character > 0 ? line.Substring(0, position.Character) : string.Empty;

            // Check if we're in an inline method context
            if (linePrefix.EndsWith("``@"))
            {
                // Suggest available classes
                var completions = _workspace.GetAvailableClasses()
                    .Select(className => new CompletionItem
                    {
                        Label = className,
                        Kind = CompletionItemKind.Class,
                        InsertTextFormat = InsertTextFormat.Snippet,
                        InsertText = $"{className}.${{1:method}}"
                    }).ToList();

                return Task.FromResult(new CompletionList(completions));
            }

            // Check if we're after a dot in an inline method
            var classNameMatch = System.Text.RegularExpressions.Regex.Match(linePrefix, @"``@(\w+)\.$");
            if (classNameMatch.Success)
            {
                var className = classNameMatch.Groups[1].Value;

                // Suggest methods for this class
                var completions = _workspace.GetMethodsForClass(className)
                    .Select(methodName => new CompletionItem
                    {
                        Label = methodName,
                        Kind = CompletionItemKind.Method,
                        InsertTextFormat = InsertTextFormat.Snippet,
                        InsertText = methodName
                    }).ToList();

                return Task.FromResult(new CompletionList(completions));
            }

            // Check if we're inside a run block (by checking previous lines)
            bool insideRunBlock = false;
            for (int i = position.Line - 1; i >= 0 && i >= position.Line - 10; i--)
            {
                if (lines[i].TrimStart().StartsWith("@run"))
                {
                    insideRunBlock = true;
                    break;
                }

                // If we hit a blank line or code that's not an annotation, stop looking
                if (string.IsNullOrWhiteSpace(lines[i]) ||
                    (!lines[i].TrimStart().StartsWith("@") && !lines[i].TrimStart().StartsWith("//")))
                {
                    break;
                }
            }

            // Add specialized completions for inside run blocks
            if (insideRunBlock && (position.Character == 0 ||
               (position.Character > 0 && linePrefix.EndsWith("@"))))
            {
                var completions = new List<CompletionItem>();

                // Include standard snippets
                completions.AddRange(GetContextualSnippets(lines));

                // Add nested class snippets when inside a run block
                completions.Add(new CompletionItem
                {
                    Label = "@varInit (nested class)",
                    Kind = CompletionItemKind.Snippet,
                    InsertTextFormat = InsertTextFormat.Snippet,
                    InsertText = "varInit(className=\"${1:ClassName}\", type=[ { name=\"${2:PropName}\", type=\"${3:string}\" }, { name=\"${4:PropName2}\", type=\"${5:string}\" } ])",
                    Documentation = new MarkupContent
                    {
                        Kind = MarkupKind.Markdown,
                        Value = "Define a nested class with properties inside a method."
                    }
                });

                return Task.FromResult(new CompletionList(completions));
            }

            // If at start of line or after @, suggest annotation snippets
            if (position.Character == 0 ||
               (position.Character > 0 && linePrefix.EndsWith("@")))
            {
                return Task.FromResult(new CompletionList(GetContextualSnippets(lines)));
            }

            // If inside parameter list, suggest parameter snippets
            if (linePrefix.Contains("(") && !linePrefix.Contains(")"))
            {
                var parameterList = new List<CompletionItem>();

                // Find which annotation we're in
                var annotationPrefix = linePrefix.Substring(0, linePrefix.LastIndexOf('('));
                var annotationType = annotationPrefix.TrimStart().Split(' ').Last().Trim();

                // Get existing parameters to avoid suggesting duplicates
                var existingParams = new HashSet<string>();
                var paramSection = linePrefix.Substring(linePrefix.LastIndexOf('(') + 1);
                var paramMatches = System.Text.RegularExpressions.Regex.Matches(paramSection, @"(\w+)=");
                foreach (System.Text.RegularExpressions.Match match in paramMatches)
                {
                    existingParams.Add(match.Groups[1].Value);
                }

                // Add parameter snippets based on the annotation type
                if (annotationType == "@varInit")
                {
                    if (!existingParams.Contains("className"))
                        parameterList.AddRange(ParameterSnippets["className"]);
                    if (!existingParams.Contains("type"))
                        parameterList.AddRange(ParameterSnippets["type"]);
                }
                else if (annotationType == "@domInit")
                {
                    if (!existingParams.Contains("target"))
                        parameterList.AddRange(ParameterSnippets["target"]);
                    if (!existingParams.Contains("tag"))
                        parameterList.AddRange(ParameterSnippets["tag"]);
                    if (!existingParams.Contains("class"))
                        parameterList.AddRange(ParameterSnippets["class"]);
                }
                else if (annotationType == "@parameter")
                {
                    if (!existingParams.Contains("name"))
                        parameterList.AddRange(ParameterSnippets["name"]);
                    if (!existingParams.Contains("type"))
                        parameterList.AddRange(ParameterSnippets["type"]);
                }
                else if (annotationType == "@onEvent")
                {
                    if (!existingParams.Contains("className"))
                        parameterList.AddRange(ParameterSnippets["className"]);
                    if (!existingParams.Contains("event"))
                        parameterList.AddRange(ParameterSnippets["event"]);
                    if (!existingParams.Contains("target"))
                        parameterList.AddRange(ParameterSnippets["target"]);
                }
                else if (annotationType == "@run")
                {
                    if (!existingParams.Contains("id"))
                        parameterList.AddRange(ParameterSnippets["id"]);
                    if (!existingParams.Contains("className"))
                        parameterList.AddRange(ParameterSnippets["className"]);
                    if (!existingParams.Contains("functionName"))
                        parameterList.AddRange(ParameterSnippets["functionName"]);
                }
                else if (annotationType == "@more")
                {
                    if (!existingParams.Contains("id"))
                    {
                        // Collect existing ids from @run blocks to suggest them
                        var existingIds = CollectExistingIds(lines);
                        if (existingIds.Any())
                        {
                            parameterList.Add(new CompletionItem
                            {
                                Label = "id",
                                Kind = CompletionItemKind.Property,
                                InsertTextFormat = InsertTextFormat.Snippet,
                                InsertText = $"id=\"${{1|{string.Join(",", existingIds)}|}}\""
                            });
                        }
                        else
                        {
                            parameterList.AddRange(ParameterSnippets["id"]);
                        }
                    }
                }
                else if (annotationType == "@condition")
                {
                    if (!existingParams.Contains("expression"))
                        parameterList.AddRange(ParameterSnippets["expression"]);
                }
                else if (annotationType == "@assert")
                {
                    if (!existingParams.Contains("className"))
                        parameterList.AddRange(ParameterSnippets["className"]);
                    if (!existingParams.Contains("condition"))
                        parameterList.AddRange(ParameterSnippets["condition"]);
                }
                else if (annotationType == "@xml")
                {
                    if (!existingParams.Contains("pageName"))
                        parameterList.AddRange(ParameterSnippets["pageName"]);
                    if (!existingParams.Contains("componentName"))
                        parameterList.AddRange(ParameterSnippets["componentName"]);
                }

                return Task.FromResult(new CompletionList(parameterList));
            }

            return Task.FromResult(new CompletionList());
        }

        private List<string> CollectExistingIds(string[] lines)
        {
            var ids = new List<string>();
            foreach (var line in lines)
            {
                if (line.TrimStart().StartsWith("@run"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"id=""([^""]+)""");
                    if (match.Success)
                    {
                        ids.Add(match.Groups[1].Value);
                    }
                }
            }
            return ids;
        }

        private List<CompletionItem> GetContextualSnippets(string[] lines)
        {
            var snippets = new List<CompletionItem>(StandardSnippets);

            // Add @more snippet if there are @run blocks with IDs
            var existingIds = CollectExistingIds(lines);
            if (existingIds.Any())
            {
                snippets.Add(new CompletionItem
                {
                    Label = "@more",
                    Kind = CompletionItemKind.Snippet,
                    InsertTextFormat = InsertTextFormat.Snippet,
                    // Remove @ from InsertText to prevent duplication
                    InsertText = $"more(id=\"${{1|{string.Join(",", existingIds)}|}}\")\n${{2:// Additional logic}}",
                    Documentation = new MarkupContent
                    {
                        Kind = MarkupKind.Markdown,
                        Value = "Add code to an existing execution block."
                    }
                });
            }

            return snippets;
        }

        protected override CompletionRegistrationOptions CreateRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities)
        {
            return new CompletionRegistrationOptions
            {
                DocumentSelector = new TextDocumentSelector(new TextDocumentFilter
                {
                    Pattern = "**/*.mrt",
                    Language = "mrt"
                }),
                TriggerCharacters = new Container<string>(new[] { "@", "(", ",", " " }),
                ResolveProvider = true
            };
        }

        public override Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken)
        {
            // This method is called when a completion item is selected
            // You can use it to add more information to the selected item
            return Task.FromResult(request);
        }
    }
}
