using MarathonTranspiler.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.React
{
    public partial class ReactTranspiler : MarathonTranspilerBase
    {
        private readonly StringBuilder _jsxOutput = new();
        private readonly HashSet<string> _imports = new() { "import React from 'react';" };
        private readonly Dictionary<string, List<string>> _customHooks = new();
        private readonly ReactConfig _config;

        public ReactTranspiler(ReactConfig config)
        {
            this._config = config;
        }

        public override string GenerateOutput()
        {
            var sb = new StringBuilder();

            // Add imports
            foreach (var import in _imports)
            {
                sb.AppendLine(import);
            }
            sb.AppendLine();

            // Generate custom hooks
            foreach (var hook in _customHooks)
            {
                sb.AppendLine($"function {hook.Key}() {{");
                foreach (var line in hook.Value)
                {
                    sb.AppendLine($"    {line}");
                }
                sb.AppendLine("}");
                sb.AppendLine();
            }

            // Generate components
            foreach (var classInfo in _classes.Values)
            {
                sb.AppendLine($"function {classInfo.ClassName}() {{");

                // State and handlers
                foreach (var line in _mainMethodLines)
                {
                    sb.AppendLine($"    {line}");
                }
                sb.AppendLine();

                // JSX
                sb.AppendLine("    return (");
                sb.AppendLine("        <div>");
                sb.Append(_jsxOutput);
                sb.AppendLine("        </div>");
                sb.AppendLine("    );");
                sb.AppendLine("}");

                sb.AppendLine($"export default {classInfo.ClassName};");
            }

            return sb.ToString();
        }
    }
}
