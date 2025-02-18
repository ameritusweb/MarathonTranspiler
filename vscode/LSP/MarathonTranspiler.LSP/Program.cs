using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Server;

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
