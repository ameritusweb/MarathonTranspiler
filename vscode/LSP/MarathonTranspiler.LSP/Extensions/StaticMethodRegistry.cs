using MarathonTranspiler.LSP.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.LSP.Extensions
{
    public class StaticMethodRegistry
    {
        private readonly Dictionary<string, Dictionary<string, MethodInfo>> _jsMethodsByClass = new();
        private readonly Dictionary<string, Dictionary<string, MethodInfo>> _csMethodsByClass = new();
        private readonly CSharpParser _csharpParser = new();
        private readonly ScriptParser _jstsParser = new();
        private bool _isInitialized = false;
        private string? _targetLanguage = null;

        public string TargetLanguage
        {
            get
            {
                return _targetLanguage ?? string.Empty;
            }
        }

        public void SetTargetLanguage(string target)
        {
            this._targetLanguage = target;
        }

        public void Initialize(string libraryDirectory)
        {
            if (_isInitialized) return;

            // Scan for C# files
            foreach (var file in Directory.GetFiles(libraryDirectory, "*.cs", SearchOption.AllDirectories))
            {
                var methods = _csharpParser.ParseFile(file);
                RegisterCsMethods(methods);
            }

            // Scan for JS files
            foreach (var file in Directory.GetFiles(libraryDirectory, "*.js", SearchOption.AllDirectories))
            {
                if (Path.DirectorySeparatorChar == '\\')
                {
                    // Windows path comparison
                    if (file.IndexOf("\\node_modules\\", StringComparison.OrdinalIgnoreCase) != -1)
                        continue;
                }
                else
                {
                    // Unix path comparison
                    if (file.IndexOf("/node_modules/", StringComparison.Ordinal) != -1)
                        continue;
                }

                var methods = _jstsParser.ParseFile(file);
                RegisterJsMethods(methods);
            }

            _isInitialized = true;
        }

        private void RegisterJsMethods(List<MethodInfo> methods)
        {
            foreach (var method in methods)
            {
                string className = method.ClassName;

                if (!_jsMethodsByClass.ContainsKey(className))
                {
                    _jsMethodsByClass[className] = new Dictionary<string, MethodInfo>();
                }

                _jsMethodsByClass[className][method.Name] = method;
            }
        }

        private void RegisterCsMethods(List<MethodInfo> methods)
        {
            foreach (var method in methods)
            {
                string className = method.ClassName;

                if (!_csMethodsByClass.ContainsKey(className))
                {
                    _csMethodsByClass[className] = new Dictionary<string, MethodInfo>();
                }

                _csMethodsByClass[className][method.Name] = method;
            }
        }

        public bool TryGetJsMethod(string className, string methodName, out MethodInfo method)
        {
            method = null;
            if (_jsMethodsByClass.TryGetValue(className, out var classMethods))
            {
                return classMethods.TryGetValue(methodName, out method);
            }
            return false;
        }

        public bool TryGetCsMethod(string className, string methodName, out MethodInfo method)
        {
            method = null;
            if (_csMethodsByClass.TryGetValue(className, out var classMethods))
            {
                return classMethods.TryGetValue(methodName, out method);
            }
            return false;
        }

        public IEnumerable<string> GetAvailableJsClasses()
        {
            return _jsMethodsByClass.Keys;
        }

        public IEnumerable<string> GetAvailableCsClasses()
        {
            return _csMethodsByClass.Keys;
        }

        public IEnumerable<MethodInfo> GetMethodsForCsClass(string className)
        {
            if (_csMethodsByClass.TryGetValue(className, out var methods))
            {
                return methods.Values.OfType<MethodInfo>();
            }
            return Enumerable.Empty<MethodInfo>();
        }

        public IEnumerable<MethodInfo> GetMethodsForJsClass(string className)
        {
            if (_jsMethodsByClass.TryGetValue(className, out var methods))
            {
                return methods.Values.OfType<MethodInfo>();
            }
            return Enumerable.Empty<MethodInfo>();
        }
    }
}
