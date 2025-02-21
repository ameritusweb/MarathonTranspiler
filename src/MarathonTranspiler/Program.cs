using MarathonTranspiler.Core;
using MarathonTranspiler.Model;
using MarathonTranspiler.Readers;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using System.Text.Json;

namespace MarathonTranspiler
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var currentDirectory = Directory.GetCurrentDirectory();

            // Path to the JSON file
            string jsonFilePath = currentDirectory + "\\mrtconfig.json";

            // Read and deserialize the JSON file
            string jsonContent = File.ReadAllText(jsonFilePath);
            Config config = JsonSerializer.Deserialize<Config>(jsonContent);

            // Create a Matcher instance
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

            // Specify the root directory for the search
            string rootDirectory = config.RootDirectory;
            var directoryInfo = new DirectoryInfoWrapper(new DirectoryInfo(rootDirectory));

            // Execute the matcher
            var matchingResult = matcher.Execute(directoryInfo);

            // Output the matched files
            foreach (var file in matchingResult.Files)
            {
                var fullPath = Path.Combine(rootDirectory, file.Path);
                var marathonReader = new MarathonReader();
                var annotatedCode = marathonReader.ReadFile(fullPath);

                var transpiler = TranspilerFactory.CreateTranspiler(config.TranspilerOptions);
                transpiler.ProcessAnnotatedCode(annotatedCode);
                var outputCode = transpiler.GenerateOutput();

                // Write the transpiled code to a .cs file
                var outputPath = Path.ChangeExtension(fullPath, ".cs");
                File.WriteAllText(outputPath, outputCode);
            }
        }
    }
}
