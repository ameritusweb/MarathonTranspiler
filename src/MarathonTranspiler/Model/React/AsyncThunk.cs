using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Model.React
{
    public class AsyncThunk
    {
        // Name of the thunk (e.g., "fetchTodos")
        public string Name { get; set; }

        // Function parameters if any
        public List<string> Parameters { get; set; } = new();

        // Which state property this affects
        public string StateProperty { get; set; }

        // The actual async operation (e.g., API request)
        public string ApiCall { get; set; }

        // Any additional logic inside the thunk
        public List<string> AdditionalCode { get; set; } = new();
    }
}
