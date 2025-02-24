using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Model.AspNet
{
    public class ControllerInfo
    {
        public string Name { get; set; }
        public List<EndpointInfo> Endpoints { get; set; } = new();
    }
}
