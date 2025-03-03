using MarathonTranspiler.Core;
using MarathonTranspiler.Extensions;
using MarathonTranspiler.Transpilers.CSharp;
using MarathonTranspiler.Transpilers.FullStackWeb;
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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MarathonTranspiler.Model
{
    public class TranspilerFactory
    {
        public static MarathonTranspilerBase CreateTranspiler(TranspilerOptions options, IStaticMethodRegistry registry)
        {
            return options.Target.ToLower() switch
            {
                "csharp" => new CSharpTranspiler(options.CSharp, registry),
                "orleans" => new OrleansTranspiler(options.Orleans),
                "unity" => new UnityTranspiler(options.Unity),
                "react" => new ReactTranspiler(options.React, registry),
                "react-redux" => new ReactReduxTranspiler(options.ReactRedux),
                "fullstackweb" => new FullStackWebTranspiler(options.FullStackWeb, registry),
                "python" => new PythonTranspiler(options.Python),
                "wpf" => new WpfTranspiler(options.Wpf),
                _ => throw new ArgumentException($"Unsupported target: {options.Target}")
            };
        }

        public static void ProcessAnnotatedCode(MarathonTranspilerBase transpiler, List<AnnotatedCode> annotatedCodes, bool validateFlows = true)
        {
            // Optional validation step
            if (validateFlows)
            {
                var validationErrors = FlowValidator.ValidateFlowReferences(annotatedCodes);
                if (validationErrors.Any())
                {
                    foreach (var error in validationErrors)
                    {
                        Console.Error.WriteLine(error);
                    }
                    throw new Exception("Flow validation failed. See error messages for details.");
                }
            }

            // Process the code
            transpiler.ProcessAnnotatedCode(annotatedCodes);
        }

        public static string StripLineNumberPrefixes(string code)
        {
            var lines = code.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var match = Regex.Match(lines[i], @"^(\s*)(\d+):(.*)$");
                if (match.Success)
                {
                    lines[i] = match.Groups[1].Value + match.Groups[3].Value;
                }
            }
            return string.Join('\n', lines);
        }
    }
}
