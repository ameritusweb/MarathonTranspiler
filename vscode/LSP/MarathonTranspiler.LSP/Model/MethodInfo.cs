using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.LSP.Model
{
    public class MethodInfo
    {
        public string Name { get; set; }
        public string Body { get; set; }
        public string FullText { get; set; }
        public List<string> Parameters { get; set; }
        public List<ParameterUsage> ParameterUsages { get; set; }
        public bool IsStatic { get; set; }
        public string SourceFile { get; set; }
        public List<string> Dependencies { get; set; } = new List<string>();
        public string ClassName { get; set; }
        public int BodyStartIndex { get; set; }
    }
}
