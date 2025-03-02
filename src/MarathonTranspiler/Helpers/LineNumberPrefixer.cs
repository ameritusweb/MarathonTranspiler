using MarathonTranspiler.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Helpers
{
    public class LineNumberPrefixer
    {
        public void AddLinePrefixes(AnnotatedCode block)
        {
            int baseLineNumber = block.StartLine + 1; // +1 to skip the annotation line

            for (int i = 0; i < block.Code.Count; i++)
            {
                string line = block.Code[i];

                // Only add prefix if it's not a control flow line (starts with --@)
                if (!line.TrimStart().StartsWith("--@"))
                {
                    // Add the line number prefix
                    block.Code[i] = $"{baseLineNumber + i}:{line}";
                }
            }
        }
    }
}
