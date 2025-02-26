using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Core
{
    public class AnnotatedCode
    {
        public List<Annotation> Annotations { get; set; } = new List<Annotation>();

        public List<string> Code { get; set; } = new List<string>();

        // Helper method to check if this block contains control flow syntax
        public bool ContainsControlFlow()
        {
            return Code?.Any(line => line.Trim().StartsWith("``@")) ?? false;
        }

        public bool ContainsDirectFlowReferences()
        {
            return Code?.Any(line => {
                var trimmed = line.Trim();
                return trimmed.StartsWith("{") && trimmed.EndsWith("}") && !trimmed.Contains(" ");
            }) ?? false;
        }

        // Helper method to extract direct flow references (only {flowName} syntax)
        public List<string> ExtractDirectFlowReferences()
        {
            var references = new List<string>();

            foreach (var line in Code ?? new List<string>())
            {
                var trimmed = line.Trim();

                // Check if this is a direct flow reference: {flowName}
                // It should start with '{', end with '}', and not contain spaces (to avoid confusing with other braces)
                if (trimmed.StartsWith("{") && trimmed.EndsWith("}") && !trimmed.Contains(" "))
                {
                    // Extract the flow name by removing the braces
                    var flowName = trimmed.Substring(1, trimmed.Length - 2).Trim();
                    references.Add(flowName);
                }
            }

            return references;
        }

        // Helper method to extract all flow references from control flow syntax lines
        public List<string> ExtractFlowReferences()
        {
            var references = new List<string>();

            foreach (var line in Code ?? new List<string>())
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("``@"))
                {
                    // Extract the flow reference {flowName} from the line
                    var startIndex = trimmed.IndexOf('{');
                    var endIndex = trimmed.IndexOf('}');

                    if (startIndex >= 0 && endIndex > startIndex)
                    {
                        var flowName = trimmed.Substring(startIndex + 1, endIndex - startIndex - 1).Trim();
                        references.Add(flowName);
                    }
                }
            }

            return references;
        }
    }
}
