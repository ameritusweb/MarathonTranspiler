using MarathonTranspiler.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MarathonTranspiler.Extensions
{
    public class StaticMethodRegistry
    {
        private readonly Dictionary<string, Dictionary<string, MethodInfo>> _methodsByClass = new();
        private readonly CSharpParser _csharpParser = new();
        private readonly ScriptParser _jstsParser = new();
        private bool _isInitialized = false;

        public void Initialize(string libraryDirectory)
        {
            if (_isInitialized) return;

            // Scan for C# files
            foreach (var file in Directory.GetFiles(libraryDirectory, "*.cs", SearchOption.AllDirectories))
            {
                var methods = _csharpParser.ParseFile(file);
                RegisterMethods(methods);
            }

            // Scan for JS/TS files
            foreach (var file in Directory.GetFiles(libraryDirectory, "*.js", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(libraryDirectory, "*.ts", SearchOption.AllDirectories)))
            {
                var methods = _jstsParser.ParseFile(file);
                RegisterMethods(methods);
            }

            _isInitialized = true;
        }

        private void RegisterMethods(List<MethodInfo> methods)
        {
            foreach (var method in methods)
            {
                string className = ExtractClassName(method.SourceFile);

                if (!_methodsByClass.ContainsKey(className))
                {
                    _methodsByClass[className] = new Dictionary<string, MethodInfo>();
                }

                _methodsByClass[className][method.Name] = method;
            }
        }

        private string ExtractClassName(string filePath)
        {
            // Simple heuristic: use the filename as class name
            return Path.GetFileNameWithoutExtension(filePath);
        }

        public bool TryGetMethod(string className, string methodName, out MethodInfo method)
        {
            method = null;
            if (_methodsByClass.TryGetValue(className, out var classMethods))
            {
                return classMethods.TryGetValue(methodName, out method);
            }
            return false;
        }

        public IEnumerable<string> GetAvailableClasses()
        {
            return _methodsByClass.Keys;
        }

        public IEnumerable<string> GetMethodsForClass(string className)
        {
            if (_methodsByClass.TryGetValue(className, out var methods))
            {
                return methods.Keys;
            }
            return Enumerable.Empty<string>();
        }
    }
}