using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Model.AspNet
{
    public class Relationship
    {
        public string SourceModel { get; set; }
        public string TargetModel { get; set; }
        public RelationType Type { get; set; }
        public string JoinModel { get; set; }
    }
}
