using ColorCode.Compilation.Languages;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using OmniSharp.Extensions.LanguageServer.Server;
using System.Diagnostics;
using System.Text;

namespace MarathonTranspiler.LSP
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var workspace = new Workspace();

            var server = await LanguageServer.From(options =>
                options.WithInput(Console.OpenStandardInput())
                       .WithOutput(Console.OpenStandardOutput())
                       .WithServices(services => services.AddSingleton(workspace))
                       .WithHandler<TextDocumentHandler>()
                       .WithHandler<CompletionHandler>()
                       .WithHandler<HoverHandler>()
                       .WithHandler<DefinitionHandler>()
                       .WithHandler<RenameHandler>()
                       .WithHandler<SemanticTokensHandler>()
                        .OnInitialize(async (server, request, token) =>
                        {
                            workspace.Initialize(server, request.RootPath);
                            workspace.SendNotification("Marathon Transpiler LSP is running.");

                            server.Register(options => options
                                .OnExecuteCommand<string>("marathon.forceCompile", (arg) => {
                                    DocumentUri uri = DocumentUri.Parse(arg);
                                    workspace.ForceCompilation(uri);
                                    return Task.FromResult(MediatR.Unit.Value);
                                })
                            );

                            await Task.CompletedTask.ConfigureAwait(false);
                        })
            );

            await server.WaitForExit;
        }
    }
}
