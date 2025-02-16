using MarathonTranspiler.Transpilers.CSharp;
using MarathonTranspiler.Transpilers.Orleans;
using MarathonTranspiler.Transpilers.Python;
using MarathonTranspiler.Transpilers.React;
using MarathonTranspiler.Transpilers.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Core
{
    public class TranspilerFactory
    {
        public static MarathonTranspilerBase CreateTranspiler(TranspilerOptions options)
        {
            return options.Target.ToLower() switch
            {
                "csharp" => new CSharpTranspiler(options.CSharp),
                "orleans" => new OrleansTranspiler(options.Orleans),
                "unity" => new UnityTranspiler(options.Unity),
                "react" => new ReactTranspiler(options.React),
                "python" => new PythonTranspiler(options.Python),
                _ => throw new ArgumentException($"Unsupported target: {options.Target}")
            };
        }
    }
}
