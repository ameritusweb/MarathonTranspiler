using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Core
{
    public class TranspiledProperty
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string StateName { get; set; }
        public string StateId { get; set; }
        public string? Code { get; set; }
    }
}
