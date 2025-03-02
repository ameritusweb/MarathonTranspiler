using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Model
{
    public class SourceMapping
    {
        public string AnnotationId { get; set; }
        public int GeneratedStartLine { get; set; }
        public int GeneratedEndLine { get; set; }
        public string BlockType { get; set; } // "run", "more", "flow", etc.
    }
}
