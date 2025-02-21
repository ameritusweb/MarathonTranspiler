using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Model.React
{
    public class StoreSlice
    {
        // Name of the Redux slice (e.g., "todos", "cart")
        public string Name { get; set; }

        // State variables and their default values
        public Dictionary<string, string> InitialState { get; set; } = new();

        // Standard Redux actions
        public List<ReduxAction> Actions { get; set; } = new();

        // Async operations (thunks)
        public List<AsyncThunk> AsyncThunks { get; set; } = new();

        // Tracks if the slice has useSelector hooks
        public bool UsesSelectors { get; set; } = false;

        // Tracks if the slice has dispatchable actions
        public bool UsesDispatch { get; set; } = false;
    }
}
