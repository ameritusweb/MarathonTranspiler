using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using MarathonTranspiler.Core;
using MarathonTranspiler.Extensions;
using MarathonTranspiler.Model;
using MarathonTranspiler.Readers;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using System.Text.Json;
using MarathonTranspiler;

namespace Marathon.CLI
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Set up the root command
            var rootCommand = new RootCommand("Marathon Transpiler CLI tool for Runtime-First development")
            {
                Name = "marathon"
            };

            // Add transpile command
            var transpileCommand = new Command("transpile", "Transpile Marathon code to target language");
            var configOption = new Option<FileInfo>(
                new[] { "--config", "-c" },
                "Path to the configuration file")
            {
                IsRequired = false
            };
            configOption.SetDefaultValue(new FileInfo("mrtconfig.json"));

            var outputOption = new Option<DirectoryInfo>(
                new[] { "--output", "-o" },
                "Output directory for generated files")
            {
                IsRequired = false
            };

            var verboseOption = new Option<bool>(
                new[] { "--verbose", "-v" },
                "Enable verbose output");

            transpileCommand.AddOption(configOption);
            transpileCommand.AddOption(outputOption);
            transpileCommand.AddOption(verboseOption);
            transpileCommand.SetHandler(TranspileHandler, configOption, outputOption, verboseOption);

            // Add watch command
            var watchCommand = new Command("watch", "Watch for file changes and transpile automatically");
            watchCommand.AddOption(configOption);
            watchCommand.AddOption(outputOption);
            watchCommand.AddOption(verboseOption);
            watchCommand.SetHandler(WatchHandler, configOption, outputOption, verboseOption);

            // Add list command
            var listCommand = new Command("list", "List available inlineable functions");
            var targetOption = new Option<string>(
                new[] { "--target", "-t" },
                "Filter functions by target (e.g., csharp, react)")
            {
                IsRequired = false
            };
            listCommand.AddOption(targetOption);
            listCommand.AddOption(verboseOption);
            listCommand.SetHandler(ListHandler, targetOption, verboseOption);

            // Add init command
            var initCommand = new Command("init", "Initialize a new Marathon project");
            var targetFrameworkOption = new Option<string>(
                new[] { "--target", "-t" },
                "Target framework (csharp, react, react-redux, orleans, etc.)")
            {
                IsRequired = true
            };
            initCommand.AddOption(targetFrameworkOption);
            initCommand.SetHandler(InitHandler, targetFrameworkOption);

            // Add all commands to root
            rootCommand.AddCommand(transpileCommand);
            rootCommand.AddCommand(watchCommand);
            rootCommand.AddCommand(listCommand);
            rootCommand.AddCommand(initCommand);

            // Parse and execute
            return await rootCommand.InvokeAsync(args);
        }

        // Handler for the transpile command
        static void TranspileHandler(FileInfo configFile, DirectoryInfo outputDir, bool verbose)
        {
            try
            {
                if (verbose)
                    Console.WriteLine($"Using configuration file: {configFile.FullName}");

                // Load and parse config
                var config = LoadConfig(configFile);

                // Initialize registry for static method inlining
                var registry = new StaticMethodRegistry();
                registry.Initialize(configFile.DirectoryName);

                // Process files
                var matcher = CreateMatcher(config);
                var rootDirectory = config.RootDirectory ?? configFile.DirectoryName;
                var directoryInfo = new DirectoryInfoWrapper(new DirectoryInfo(rootDirectory));
                var matchingResult = matcher.Execute(directoryInfo);

                if (!matchingResult.HasMatches)
                {
                    Console.WriteLine("No files matched the include patterns in the configuration.");
                    return;
                }

                int fileCount = 0;
                foreach (var file in matchingResult.Files)
                {
                    var fullPath = Path.Combine(rootDirectory, file.Path);
                    if (verbose)
                        Console.WriteLine($"Transpiling: {file.Path}");

                    var marathonReader = new MarathonReader();
                    var annotatedCode = marathonReader.ReadFile(fullPath);

                    var transpiler = TranspilerFactory.CreateTranspiler(config.TranspilerOptions, registry);
                    TranspilerFactory.ProcessAnnotatedCode(transpiler, annotatedCode, true);
                    var outputCode = transpiler.GenerateOutput();

                    // Determine output path
                    string outputPath;
                    if (outputDir != null)
                    {
                        var relativePath = file.Path;
                        outputPath = Path.Combine(outputDir.FullName, Path.ChangeExtension(relativePath, DetermineExtension(config.TranspilerOptions.Target)));

                        // Ensure directory exists
                        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                    }
                    else
                    {
                        outputPath = Path.ChangeExtension(fullPath, DetermineExtension(config.TranspilerOptions.Target));
                    }

                    File.WriteAllText(outputPath, outputCode);
                    fileCount++;
                }

                Console.WriteLine($"Transpilation complete. {fileCount} files processed.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Error during transpilation: {ex.Message}");
                if (verbose)
                    Console.Error.WriteLine(ex.StackTrace);
                Console.ResetColor();
            }
        }

        // Handler for the watch command
        static void WatchHandler(FileInfo configFile, DirectoryInfo outputDir, bool verbose)
        {
            Console.WriteLine("Starting watch mode...");

            try
            {
                // Load and parse config
                var config = LoadConfig(configFile);
                var rootDirectory = config.RootDirectory ?? configFile.DirectoryName;

                using var watcher = new FileSystemWatcher(rootDirectory);

                // Set up event handlers
                watcher.Changed += (sender, e) => OnFileChanged(e.FullPath, configFile, outputDir, verbose);
                watcher.Created += (sender, e) => OnFileChanged(e.FullPath, configFile, outputDir, verbose);

                // Set up watcher configuration
                watcher.Filter = "*.mrt";
                watcher.IncludeSubdirectories = true;
                watcher.EnableRaisingEvents = true;

                Console.WriteLine($"Watching for changes in {rootDirectory}");
                Console.WriteLine("Press Ctrl+C to exit");

                // Keep the program running
                ManualResetEvent exitEvent = new ManualResetEvent(false);
                Console.CancelKeyPress += (sender, e) => {
                    e.Cancel = true;
                    exitEvent.Set();
                };

                exitEvent.WaitOne();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Error in watch mode: {ex.Message}");
                if (verbose)
                    Console.Error.WriteLine(ex.StackTrace);
                Console.ResetColor();
            }
        }

        // Handler for file changes in watch mode
        static void OnFileChanged(string fullPath, FileInfo configFile, DirectoryInfo outputDir, bool verbose)
        {
            try
            {
                if (!File.Exists(fullPath) || !fullPath.EndsWith(".mrt"))
                    return;

                Console.WriteLine($"File changed: {Path.GetFileName(fullPath)}");

                // Add a small delay to ensure file is not locked
                Thread.Sleep(100);

                // Load config
                var config = LoadConfig(configFile);
                var rootDirectory = config.RootDirectory ?? configFile.DirectoryName;

                // Check if file matches include/exclude patterns
                var matcher = CreateMatcher(config);
                var relativePath = Path.GetRelativePath(rootDirectory, fullPath);

                if (!matcher.Match(relativePath).HasMatches)
                {
                    if (verbose)
                        Console.WriteLine($"File {relativePath} does not match include/exclude patterns. Skipping.");
                    return;
                }

                // Initialize registry
                var registry = new StaticMethodRegistry();
                registry.Initialize(configFile.DirectoryName);

                // Process file
                if (verbose)
                    Console.WriteLine($"Transpiling: {relativePath}");

                var marathonReader = new MarathonReader();
                var annotatedCode = marathonReader.ReadFile(fullPath);

                var transpiler = TranspilerFactory.CreateTranspiler(config.TranspilerOptions, registry);
                TranspilerFactory.ProcessAnnotatedCode(transpiler, annotatedCode, true);
                var outputCode = transpiler.GenerateOutput();

                // Determine output path
                string outputPath;
                if (outputDir != null)
                {
                    outputPath = Path.Combine(outputDir.FullName, Path.ChangeExtension(relativePath, DetermineExtension(config.TranspilerOptions.Target)));
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                }
                else
                {
                    outputPath = Path.ChangeExtension(fullPath, DetermineExtension(config.TranspilerOptions.Target));
                }

                File.WriteAllText(outputPath, outputCode);
                Console.WriteLine($"Transpiled: {Path.GetFileName(fullPath)} -> {Path.GetFileName(outputPath)}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Error processing file {fullPath}: {ex.Message}");
                Console.ResetColor();
            }
        }

        // Handler for the list command
        static void ListHandler(string target, bool verbose)
        {
            try
            {
                Console.WriteLine("Available inlineable functions:");
                Console.WriteLine("===============================");

                var registry = new StaticMethodRegistry();
                registry.Initialize(Directory.GetCurrentDirectory());

                var classes = registry.GetAvailableClasses();

                foreach (var className in classes)
                {
                    if (!string.IsNullOrEmpty(target) && !className.Contains(target, StringComparison.OrdinalIgnoreCase))
                        continue;

                    Console.WriteLine($"\n{className}:");
                    var methods = registry.GetMethodsForClass(className);

                    foreach (var method in methods)
                    {
                        Console.WriteLine($"  - {method}");
                        if (verbose)
                        {
                            // If verbose, try to get method info and show parameter info
                            if (registry.TryGetMethod(className, method, out var methodInfo))
                            {
                                Console.WriteLine($"    Parameters: {string.Join(", ", methodInfo.Parameters)}");
                                foreach (var dependency in methodInfo.Dependencies)
                                {
                                    Console.WriteLine($"    Dependency: {dependency}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Error listing functions: {ex.Message}");
                Console.ResetColor();
            }
        }

        // Handler for the init command
        static void InitHandler(string targetFramework)
        {
            try
            {
                Console.WriteLine($"Initializing new Marathon project with target: {targetFramework}");

                var configPath = Path.Combine(Directory.GetCurrentDirectory(), "mrtconfig.json");
                if (File.Exists(configPath))
                {
                    Console.WriteLine("A configuration file already exists. Overwrite? (y/n)");
                    var key = Console.ReadKey(true);
                    if (key.KeyChar != 'y' && key.KeyChar != 'Y')
                    {
                        Console.WriteLine("Initialization cancelled.");
                        return;
                    }
                }

                // Create basic config based on target
                var config = new Config
                {
                    TranspilerOptions = new TranspilerOptions
                    {
                        Target = targetFramework
                    },
                    Include = new List<string> { "**/*.mrt" },
                    Exclude = new List<string> { "**/node_modules/**", "**/bin/**", "**/obj/**" },
                    RootDirectory = "."
                };

                // Set specific config options based on target
                switch (targetFramework.ToLower())
                {
                    case "csharp":
                        config.TranspilerOptions.CSharp = new MarathonTranspiler.Transpilers.CSharp.CSharpConfig
                        {
                            TestFramework = "xunit"
                        };
                        break;
                    case "react":
                        config.TranspilerOptions.React = new MarathonTranspiler.Transpilers.React.ReactConfig
                        {
                            UseTypeScript = false,
                            TestFramework = "jest"
                        };
                        break;
                    case "react-redux":
                        config.TranspilerOptions.ReactRedux = new MarathonTranspiler.Transpilers.ReactRedux.ReactReduxConfig
                        {
                            Name = "Marathon App",
                            DevTools = true
                        };
                        break;
                    case "orleans":
                        config.TranspilerOptions.Orleans = new MarathonTranspiler.Transpilers.Orleans.OrleansConfig
                        {
                            Stateful = true
                        };
                        break;
                        // Add more target-specific configuration as needed
                }

                // Serialize and save config
                var options = new JsonSerializerOptions { WriteIndented = true };
                var jsonString = JsonSerializer.Serialize(config, options);
                File.WriteAllText(configPath, jsonString);

                // Create example file
                var exampleDir = Path.Combine(Directory.GetCurrentDirectory(), "src");
                Directory.CreateDirectory(exampleDir);
                var examplePath = Path.Combine(exampleDir, "Example.mrt");

                var exampleContent = GenerateExampleForTarget(targetFramework);
                File.WriteAllText(examplePath, exampleContent);

                Console.WriteLine("Marathon project initialized successfully!");
                Console.WriteLine($"Configuration saved to: {configPath}");
                Console.WriteLine($"Example file created at: {examplePath}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Error during initialization: {ex.Message}");
                Console.ResetColor();
            }
        }

        // Helper method to load config
        static Config LoadConfig(FileInfo configFile)
        {
            if (!configFile.Exists)
                throw new FileNotFoundException($"Configuration file not found: {configFile.FullName}");

            string jsonContent = File.ReadAllText(configFile.FullName);
            return JsonSerializer.Deserialize<Config>(jsonContent)
                ?? throw new InvalidOperationException("Failed to parse configuration file.");
        }

        // Helper method to create matcher from config
        static Matcher CreateMatcher(Config config)
        {
            var matcher = new Matcher();

            // Add include patterns
            foreach (var includePattern in config.Include)
            {
                matcher.AddInclude(includePattern);
            }

            // Add exclude patterns
            foreach (var excludePattern in config.Exclude)
            {
                matcher.AddExclude(excludePattern);
            }

            return matcher;
        }

        // Helper method to determine file extension based on target
        static string DetermineExtension(string target)
        {
            return target.ToLower() switch
            {
                "csharp" => ".cs",
                "react" => ".jsx",
                "react-redux" => ".jsx",
                "fullstackweb" => ".js", // Or may need to handle this specially
                "python" => ".py",
                "unity" => ".cs",
                "orleans" => ".cs",
                "wpf" => ".cs",
                _ => ".txt"
            };
        }

        // Helper method to generate example content based on target
        static string GenerateExampleForTarget(string target)
        {
            switch (target.ToLower())
            {
                case "csharp":
                    return @"@varInit(className=""Counter"", type=""int"")
this.count = 0;

@run(className=""Counter"", functionName=""increment"", id=""inc1"")
this.count++;
Console.WriteLine($""Count: {this.count}"");

@run(className=""Counter"", functionName=""reset"")
this.count = 0;

@assert(className=""Counter"", condition=""this.count >= 0"")
""Count should never be negative""";

                case "react":
                    return @"@varInit(className=""Counter"", type=""int"", hookName=""useState"")
this.count = 0;

@run(className=""Counter"", functionName=""increment"", id=""inc1"")
this.count = this.count + 1;

@run(className=""Counter"", functionName=""reset"")
this.count = 0;

@xml(componentName=""Counter"")
<Component>
  <h1>Counter: {count}</h1>
  <button onClick={increment}>Increment</button>
  <button onClick={reset}>Reset</button>
</Component>";

                case "react-redux":
                    return @"@varInit(className=""counterSlice"", type=""int"")
this.count = 0;

@run(className=""counterSlice"", functionName=""increment"")
this.count = this.count + 1;

@run(className=""counterSlice"", functionName=""decrement"")
this.count = this.count - 1;

@run(className=""counterSlice"", functionName=""reset"")
this.count = 0;

@xml(componentName=""Counter"")
<Component>
  <Prop name=""count"" reduxState=""true"" />
  <div>
    <h1>Counter: {count}</h1>
    <button onClick={() => dispatch(increment())}>+</button>
    <button onClick={() => dispatch(decrement())}>-</button>
    <button onClick={() => dispatch(reset())}>Reset</button>
  </div>
</Component>";

                case "orleans":
                    return @"@varInit(className=""CounterGrain"", type=""int"", stateName=""Count"")
this.count = 0;

@run(className=""CounterGrain"", functionName=""Increment"")
this.count++;
return this.count;

@run(className=""CounterGrain"", functionName=""GetCount"")
return this.count;

@run(className=""CounterGrain"", functionName=""Reset"")
this.count = 0;
return Task.CompletedTask;";

                default:
                    return @"@varInit(className=""Example"", type=""string"")
this.message = ""Hello from Marathon!"";

@run(className=""Example"", functionName=""sayHello"")
Console.WriteLine(this.message);

@assert(className=""Example"", condition=""this.message.Length > 0"")
""Message should not be empty""";
            }
        }
    }
}