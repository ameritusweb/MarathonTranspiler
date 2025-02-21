using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Model.React
{
    public class ReduxAction
    {
        // Name of the action (e.g., "addTodo")
        public string Name { get; set; }

        // Parameters for the action
        public List<string> Parameters { get; set; } = new();

        // The code that runs when action is dispatched
        public List<string> Code { get; set; } = new();
    }

}
