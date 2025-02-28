using MarathonTranspiler.Core;
using MarathonTranspiler.Extensions;
using MarathonTranspiler.Model;
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

        public void ProcessInlining(AnnotatedCode block)
        {
            if (block.Code.Any() && block.Code.Any(line => line.Contains("``@")))
            {
                for (int i = 0; i < block.Code.Count; i++)
                {
                    if (block.Code[i].Contains("``@"))
                    {
                        List<string> dependencies;
                        block.Code[i] = _methodInliner.ProcessInlining(block.Code[i], out dependencies);

                        // Handle dependencies by adding them to the class
                        foreach (var dependency in dependencies)
                        {
                            // Check if it's a using statement
                            if (dependency.StartsWith("using "))
                            {
                                if (!block.AdditionalData.ContainsKey("usings"))
                                {
                                    block.AdditionalData["usings"] = new List<string>();
                                }

                                var usings = (List<string>)block.AdditionalData["usings"];
                                if (!usings.Contains(dependency))
                                {
                                    usings.Add(dependency);
                                }
                            }
                            // Check if it's an import
                            else if (dependency.StartsWith("import "))
                            {
                                if (!block.AdditionalData.ContainsKey("imports"))
                                {
                                    block.AdditionalData["imports"] = new List<string>();
                                }

                                var imports = (List<string>)block.AdditionalData["imports"];
                                if (!imports.Contains(dependency))
                                {
                                    imports.Add(dependency);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
