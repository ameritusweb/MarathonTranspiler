using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Threading;
using System.Threading.Tasks;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace MarathonTranspiler.LSP
{
    public class DefinitionHandler : DefinitionHandlerBase
    {
        public override Task<LocationOrLocationLinks> Handle(DefinitionParams request, CancellationToken cancellationToken)
        {
            List<LocationOrLocationLink> links = new List<LocationOrLocationLink>();
            var textDocument = request.TextDocument;
            var position = request.Position;

            links.Add(new LocationOrLocationLink(new Location()
            {
                Uri = request.TextDocument.Uri,
                Range = new Range(new Position(0, 0), new Position(0, 10))
            }));

            return Task.FromResult<LocationOrLocationLinks>(links);
        }

        protected override DefinitionRegistrationOptions CreateRegistrationOptions(DefinitionCapability capability, ClientCapabilities clientCapabilities)
        {
            DefinitionRegistrationOptions options = new DefinitionRegistrationOptions();
            List<TextDocumentFilter> filters = new List<TextDocumentFilter>();
            options.DocumentSelector = new TextDocumentSelector(new TextDocumentFilter
            {
                Pattern = "**/*.mrt",
                Language = "mrt"
            });
            return options;
        }
    }
}
