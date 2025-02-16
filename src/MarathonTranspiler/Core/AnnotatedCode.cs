using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Core
{
    public class AnnotatedCode
    {
        public List<Annotation> Annotations { get; set; } = new List<Annotation>();

        public List<string> Code { get; set; } = new List<string>();
    }
}
