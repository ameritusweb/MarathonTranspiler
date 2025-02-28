using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Model
{
    public class InlineMethodCall
    {
        public string ClassName { get; set; }
        public string MethodName { get; set; }
        public List<string> Arguments { get; set; }
        public string FullMatch { get; set; }
        public int StartIndex { get; set; }
        public int Length { get; set; }
    }
}
