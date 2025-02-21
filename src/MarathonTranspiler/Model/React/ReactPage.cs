using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Model.React
{
    public class ReactPage : TranspiledPage
    {
        public List<string> LayoutComponents { get; set; } = new();
        public Dictionary<string, string> PageMetadata { get; set; } = new();
    }
}
