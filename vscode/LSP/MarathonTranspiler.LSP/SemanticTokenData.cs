using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.LSP
{
    public class SemanticTokenData
    {
        public int Line { get; set; }
        public int Column { get; set; }
        public int Length { get; set; }
        public string Token { get; set; }
        public SemanticTokenType TokenType { get; set; }
        public SemanticTokenModifier[] Modifiers { get; set; }
    }
}
