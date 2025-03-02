using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.LSP
{
    public class CommandHandler : ExecuteCommandHandlerBase
    {
        private readonly Workspace _workspace;

        public CommandHandler(Workspace workspace)
        {
            _workspace = workspace;
        }

        public override Task<MediatR.Unit> Handle(ExecuteCommandParams request, CancellationToken cancellationToken)
        {
            if (request.Command == "marathon.forceCompile")
            {
                if (request.Arguments?.Count > 0 &&
                    request.Arguments[0] is JToken uriToken)
                {
                    DocumentUri uri = DocumentUri.Parse(uriToken.ToString());

                    // Force immediate compilation
                    _workspace.ForceCompilation(uri);
                }
            }

            return Task.FromResult(MediatR.Unit.Value);
        }

        protected override ExecuteCommandRegistrationOptions CreateRegistrationOptions(ExecuteCommandCapability capability, ClientCapabilities clientCapabilities)
        {
            return new ExecuteCommandRegistrationOptions()
            {
                Commands = new Container<string>(new[] { "marathon.forceCompile" })
            };
        }
    }
}
