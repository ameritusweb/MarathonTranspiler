using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Model
{
    public class ParameterUsage
    {
        public string Name { get; set; }
        public List<int> Locations { get; set; } = new List<int>();
    }
}
