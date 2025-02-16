using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Core
{
    public class TranspiledClass
    {
        public string ClassName { get; set; }
        public List<string> Fields { get; set; } = new();
        public List<TranspiledProperty> Properties { get; set; } = new();
        public List<TranspiledMethod> Methods { get; set; } = new();
        public List<string> ConstructorLines { get; set; } = new();
        public List<string> Assertions { get; set; } = new();
    }
}
