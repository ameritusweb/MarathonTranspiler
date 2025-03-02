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
        private List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        private string _libraryDirectory;

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

        private void SetupFileWatchers(string libraryDirectory)
        {
            _libraryDirectory = libraryDirectory;

            // Create watcher for C# files
            var csWatcher = new FileSystemWatcher(libraryDirectory)
            {
                Filter = "*.cs",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.CreationTime
            };

            // Create watcher for JS files
            var jsWatcher = new FileSystemWatcher(libraryDirectory)
            {
                Filter = "*.js",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.CreationTime
            };

            // Set up event handlers
            csWatcher.Changed += OnCSharpFileChanged;
            csWatcher.Created += OnCSharpFileChanged;
            csWatcher.Deleted += OnCSharpFileDeleted;

            jsWatcher.Changed += OnJavaScriptFileChanged;
            jsWatcher.Created += OnJavaScriptFileChanged;
            jsWatcher.Deleted += OnJavaScriptFileDeleted;

            // Enable the watchers
            csWatcher.EnableRaisingEvents = true;
            jsWatcher.EnableRaisingEvents = true;

            // Store the watchers to keep them alive
            _watchers.Add(csWatcher);
            _watchers.Add(jsWatcher);
        }

        public void Initialize(string libraryDirectory)
        {
            _libraryDirectory = libraryDirectory;

            if (_isInitialized)
            {
                // If already initialized, just update the directory and refresh watchers
                UpdateWatchers(libraryDirectory);
                return;
            }

            // Scan for C# files
            foreach (var file in Directory.GetFiles(libraryDirectory, "*.cs", SearchOption.AllDirectories))
            {
                ProcessCSharpFile(file);
            }

            // Scan for JS files
            foreach (var file in Directory.GetFiles(libraryDirectory, "*.js", SearchOption.AllDirectories))
            {
                ProcessJavaScriptFile(file);
            }

            // Set up file watchers
            SetupFileWatchers(libraryDirectory);

            _isInitialized = true;
        }

        private void UpdateWatchers(string libraryDirectory)
        {
            // Remove existing watchers
            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();

            // Set up new watchers
            SetupFileWatchers(libraryDirectory);
        }

        // Process a C# file
        private void ProcessCSharpFile(string file)
        {
            try
            {
                var methods = _csharpParser.ParseFile(file);
                RegisterCsMethods(methods);
            }
            catch (Exception ex)
            {
                // Log the error but continue processing
                Console.Error.WriteLine($"Error processing C# file {file}: {ex.Message}");
            }
        }

        // Process a JavaScript file
        private void ProcessJavaScriptFile(string file)
        {
            try
            {
                // Skip node_modules
                if (file.Contains("node_modules"))
                    return;

                var methods = _jstsParser.ParseFile(file);
                RegisterJsMethods(methods);
            }
            catch (Exception ex)
            {
                // Log the error but continue processing
                Console.Error.WriteLine($"Error processing JavaScript file {file}: {ex.Message}");
            }
        }

        // Handle C# file changes
        private void OnCSharpFileChanged(object sender, FileSystemEventArgs e)
        {
            // Use Task.Run to avoid blocking the file system watcher thread
            Task.Run(() => {
                try
                {
                    // Add a small delay to ensure the file is not locked
                    Thread.Sleep(100);

                    if (File.Exists(e.FullPath))
                    {
                        // Remove any previous methods from this file
                        RemoveMethodsFromFile(e.FullPath, isCSharp: true);

                        // Process the updated file
                        ProcessCSharpFile(e.FullPath);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error handling C# file change: {ex.Message}");
                }
            });
        }

        // Handle JavaScript file changes
        private void OnJavaScriptFileChanged(object sender, FileSystemEventArgs e)
        {
            // Skip node_modules
            if (e.FullPath.Contains("node_modules"))
                return;

            // Use Task.Run to avoid blocking the file system watcher thread
            Task.Run(() => {
                try
                {
                    // Add a small delay to ensure the file is not locked
                    Thread.Sleep(100);

                    if (File.Exists(e.FullPath))
                    {
                        // Remove any previous methods from this file
                        RemoveMethodsFromFile(e.FullPath, isCSharp: false);

                        // Process the updated file
                        ProcessJavaScriptFile(e.FullPath);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error handling JavaScript file change: {ex.Message}");
                }
            });
        }

        // Handle C# file deletions
        private void OnCSharpFileDeleted(object sender, FileSystemEventArgs e)
        {
            Task.Run(() => {
                RemoveMethodsFromFile(e.FullPath, isCSharp: true);
            });
        }

        // Handle JavaScript file deletions
        private void OnJavaScriptFileDeleted(object sender, FileSystemEventArgs e)
        {
            if (e.FullPath.Contains("node_modules"))
                return;

            Task.Run(() => {
                RemoveMethodsFromFile(e.FullPath, isCSharp: false);
            });
        }

        // Remove methods associated with a specific file
        private void RemoveMethodsFromFile(string filePath, bool isCSharp)
        {
            if (isCSharp)
            {
                // Remove C# methods for this file
                foreach (var className in _csMethodsByClass.Keys.ToList())
                {
                    var methods = _csMethodsByClass[className];
                    foreach (var methodName in methods.Keys.ToList())
                    {
                        if (methods[methodName].SourceFile == filePath)
                        {
                            methods.Remove(methodName);
                        }
                    }

                    // Remove class if it has no more methods
                    if (methods.Count == 0)
                    {
                        _csMethodsByClass.Remove(className);
                    }
                }
            }
            else
            {
                // Remove JavaScript methods for this file
                foreach (var className in _jsMethodsByClass.Keys.ToList())
                {
                    var methods = _jsMethodsByClass[className];
                    foreach (var methodName in methods.Keys.ToList())
                    {
                        if (methods[methodName].SourceFile == filePath)
                        {
                            methods.Remove(methodName);
                        }
                    }

                    // Remove class if it has no more methods
                    if (methods.Count == 0)
                    {
                        _jsMethodsByClass.Remove(className);
                    }
                }
            }
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
