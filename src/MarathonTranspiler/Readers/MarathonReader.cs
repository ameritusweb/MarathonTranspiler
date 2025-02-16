using MarathonTranspiler.Core;
using System.Text.RegularExpressions;

namespace MarathonTranspiler.Readers
{
    public class MarathonReader
    {
        private static readonly Regex AnnotationRegex = new(@"^@(\w+)\((.*)\)$");
        private static readonly Regex KeyValueRegex = new(@"(\w+)=""([^""]*)"",?");

        public List<AnnotatedCode> ParseFile(List<string> lines)
        {
            var annotatedCodes = new List<AnnotatedCode>();
            var currentAnnotation = new Annotation();
            var currentCode = new List<string>();
            var currentBlock = new AnnotatedCode();

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Skip empty lines
                if (string.IsNullOrWhiteSpace(trimmedLine))
                    continue;

                var annotationMatch = AnnotationRegex.Match(trimmedLine);
                if (annotationMatch.Success)
                {
                    // If we have existing code, save it to the current block
                    if (currentCode.Count > 0)
                    {
                        currentBlock.Code = new List<string>(currentCode);
                        annotatedCodes.Add(currentBlock);

                        // Reset for new block
                        currentBlock = new AnnotatedCode();
                        currentCode.Clear();
                    }

                    // Parse the annotation
                    currentAnnotation = new Annotation
                    {
                        Name = annotationMatch.Groups[1].Value,
                        Values = new List<KeyValuePair<string, string>>()
                    };

                    // Parse key-value pairs
                    var keyValueContent = annotationMatch.Groups[2].Value;
                    var keyValueMatches = KeyValueRegex.Matches(keyValueContent);

                    foreach (Match kvMatch in keyValueMatches)
                    {
                        var key = kvMatch.Groups[1].Value;
                        var value = kvMatch.Groups[2].Value;
                        currentAnnotation.Values.Add(new KeyValuePair<string, string>(key, value));
                    }

                    currentBlock.Annotations.Add(currentAnnotation);
                }
                else
                {
                    // Add code line
                    currentCode.Add(trimmedLine);
                }
            }

            // Don't forget to add the last block if it contains code
            if (currentCode.Count > 0)
            {
                currentBlock.Code = new List<string>(currentCode);
                annotatedCodes.Add(currentBlock);
            }

            return annotatedCodes;
        }

        public List<AnnotatedCode> ReadFile(string filePath)
        {
            var lines = File.ReadAllLines(filePath).ToList();
            return ParseFile(lines);
        }
    }
}