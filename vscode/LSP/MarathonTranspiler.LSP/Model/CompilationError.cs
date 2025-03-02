using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.LSP.Model
{
    public class CompilationError
    {
        public string Message { get; set; }

        public int MarathonLine { get; set; }

        public string Severity { get; set; }

        public string SourceAnnotation { get; set; }
    }
}
