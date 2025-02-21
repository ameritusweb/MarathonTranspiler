using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Model
{
    public class TranspiledPage : TranspiledComponent
    {
        public string Route { get; set; }
        public bool IsSecure { get; set; }
        public Dictionary<string, string> RouteParameters { get; set; } = new();
    }
}
