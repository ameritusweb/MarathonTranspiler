using MarathonTranspiler.Core;
using System.Text.RegularExpressions;

namespace MarathonTranspiler.Readers
{
    public class MarathonReader
    {
        private static readonly Regex AnnotationRegex = new(@"^@(\w+)\((.*)\)$");
        private static readonly Regex StandardKeyValueRegex = new(@"(\w+)=""([^""]*)"",?");

        // New regex for complex type array syntax in varInit
        private static readonly Regex TypeArrayRegex = new(@"type=(\[.*?\])");

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

                    // Special handling for varInit with complex type array
                    if (currentAnnotation.Name == "varInit")
                    {
                        var typeArrayMatch = TypeArrayRegex.Match(keyValueContent);
                        if (typeArrayMatch.Success)
                        {
                            // Extract the array content
                            var arrayContent = typeArrayMatch.Groups[1].Value;

                            // Add the type array as a value
                            currentAnnotation.Values.Add(new KeyValuePair<string, string>("type", arrayContent));

                            // Remove the type array from the content so we can parse other key-value pairs
                            keyValueContent = TypeArrayRegex.Replace(keyValueContent, "");

                            // Parse remaining standard key-value pairs
                            var keyValueMatches = StandardKeyValueRegex.Matches(keyValueContent);
                            foreach (Match kvMatch in keyValueMatches)
                            {
                                var key = kvMatch.Groups[1].Value;
                                var value = kvMatch.Groups[2].Value;

                                // Skip type key since we already processed it
                                if (key != "type")
                                {
                                    currentAnnotation.Values.Add(new KeyValuePair<string, string>(key, value));
                                }
                            }
                        }
                        else
                        {
                            // Standard key-value parsing for regular varInit
                            var keyValueMatches = StandardKeyValueRegex.Matches(keyValueContent);
                            foreach (Match kvMatch in keyValueMatches)
                            {
                                var key = kvMatch.Groups[1].Value;
                                var value = kvMatch.Groups[2].Value;
                                currentAnnotation.Values.Add(new KeyValuePair<string, string>(key, value));
                            }
                        }
                    }
                    else
                    {
                        // Standard key-value parsing for all other annotations
                        var keyValueMatches = StandardKeyValueRegex.Matches(keyValueContent);
                        foreach (Match kvMatch in keyValueMatches)
                        {
                            var key = kvMatch.Groups[1].Value;
                            var value = kvMatch.Groups[2].Value;
                            currentAnnotation.Values.Add(new KeyValuePair<string, string>(key, value));
                        }
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