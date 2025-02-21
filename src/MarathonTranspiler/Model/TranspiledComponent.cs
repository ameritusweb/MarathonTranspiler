using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Model
{
    public class TranspiledComponent
    {
        public string Name { get; set; }
        public List<string> Props { get; set; } = new();
        public HashSet<string> RequiredImports { get; set; } = new();
        public List<string> Code { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
