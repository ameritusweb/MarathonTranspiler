using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Model.React
{
    public class ReactComponent : TranspiledComponent
    {
        public StoreSlice ReduxSlice { get; set; }
        public bool IsReduxConnected => ReduxSlice != null;
        public List<Assertion> Assertions { get; set; } = new();
        public Dictionary<string, string> DomAttributes { get; set; } = new();
        public List<ReactComponent> Children { get; set; } = new();
    }
}
