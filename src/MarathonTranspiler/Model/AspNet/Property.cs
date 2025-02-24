using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Model.AspNet
{
    public class Property
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsNavigation { get; set; }
        public bool IsCollection { get; set; }
    }
}
