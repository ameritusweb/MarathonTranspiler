using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Model.AspNet
{
    public class EndpointInfo
    {
        public string Name { get; set; }
        public string HttpMethod { get; set; }
        public string Route { get; set; }
        public List<string> Code { get; set; }
        public List<ParameterInfo> Parameters { get; set; } = new();
    }
}
