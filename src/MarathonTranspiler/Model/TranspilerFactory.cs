using MarathonTranspiler.Core;
using MarathonTranspiler.Transpilers.CSharp;
using MarathonTranspiler.Transpilers.Orleans;
using MarathonTranspiler.Transpilers.Python;
using MarathonTranspiler.Transpilers.React;
using MarathonTranspiler.Transpilers.ReactRedux;
using MarathonTranspiler.Transpilers.Unity;
using MarathonTranspiler.Transpilers.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Model
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
                "react-redux" => new ReactReduxTranspiler(options.ReactRedux),
                "python" => new PythonTranspiler(options.Python),
                "wpf" => new WpfTranspiler(options.Wpf),
                _ => throw new ArgumentException($"Unsupported target: {options.Target}")
            };
        }
    }
}
