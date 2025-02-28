using MarathonTranspiler.Core;
using MarathonTranspiler.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Helpers
{
    public class StaticMethodInliningHelper
    {
        private readonly StaticMethodRegistry _methodRegistry;
        private readonly StaticMethodInliner _methodInliner;

        public StaticMethodInliningHelper(StaticMethodRegistry registry)
        {
            _methodRegistry = registry;
            _methodInliner = new StaticMethodInliner(registry);
        }

        public void ProcessInlining(AnnotatedCode block, string targetLanguage)
        {
            if (block.Code.Any() && block.Code.Any(line => line.Contains("``@")))
            {
                for (int i = 0; i < block.Code.Count; i++)
                {
                    if (block.Code[i].Contains("``@"))
                    {
                        block.Code[i] = _methodInliner.ProcessInlining(block.Code[i], targetLanguage);
                    }
                }
            }
        }
    }
}
