using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Core
{
    public class InjectedDependency
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public string ParameterName => Name.TrimStart('_'); // Convert _logger to logger for parameter
    }
}
