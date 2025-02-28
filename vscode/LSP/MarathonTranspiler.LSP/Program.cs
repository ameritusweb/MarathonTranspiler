using ColorCode.Compilation.Languages;
using Microsoft.Extensions.DependencyInjection;
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

            while (!Debugger.IsAttached)
            {
                await Task.Delay(100); // Keep waiting until debugger is attached
            }

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
                        await Task.CompletedTask.ConfigureAwait(false);
                    })
            );

            await server.WaitForExit;
        }
    }
}
