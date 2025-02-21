using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Model.React
{
    public class Assertion
    {
        // Component being tested
        public string ClassName { get; set; }

        // The test condition
        public string Condition { get; set; }

        // Test message/description
        public string Message { get; set; }

        // Action to test (if any)
        public string Action { get; set; }
    }
}
