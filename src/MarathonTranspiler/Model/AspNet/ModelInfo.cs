using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Model.AspNet
{
    public class ModelInfo
    {
        public string Name { get; set; }
        public List<PropertyInfo> Properties { get; set; } = new();
    }
}
