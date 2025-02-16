using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Core
{
    public class Annotation
    {
        public string Name { get; set; }

        public List<KeyValuePair<string, string>> Values { get; set; } = new List<KeyValuePair<string, string>>();
    }
}
