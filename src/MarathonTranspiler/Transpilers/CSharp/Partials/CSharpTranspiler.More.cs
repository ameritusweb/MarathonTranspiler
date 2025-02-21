using MarathonTranspiler.Core;
using MarathonTranspiler.Extensions;
using MarathonTranspiler.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.CSharp
{
    public partial class CSharpTranspiler : MarathonTranspilerBase
    {
        protected override void ProcessMore(TranspiledClass currentClass, AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var id = annotation.Values.First(v => v.Key == "id").Value;
            var method = currentClass.Methods.FirstOrDefault(m => m.Id == id);
            int insertIndex = -1;

            if (method == null)
            {
                method = currentClass.Methods.FirstOrDefault(x => x.IndexById.ContainsKey(id));
                if (method != null)
                {
                    insertIndex = method.IndexById[id];
                }
            }
            else
            {
                insertIndex = method.Code.Count;
            }

            if (method != null && insertIndex != -1)
            {
                if (block.Annotations.Any(a => a.Name == "condition"))
                {
                    var conditionAnnotation = block.Annotations.First(x => x.Name == "condition");
                    var expression = conditionAnnotation.Values.First(v => v.Key == "expression").Value;
                    var conditionId = conditionAnnotation.Values.GetValue("id");

                    List<string> cblock = new List<string>();
                    cblock.Add($"if ({expression})");
                    cblock.Add("{");
                    cblock.AddRange(block.Code.Select(line => $"\t{line}"));
                    cblock.Add("}");

                    // Calculate how many lines we're about to insert
                    int insertedLines = cblock.Count;

                    // Adjust all subsequent indexes
                    foreach (var kvp in method.IndexById.ToList())
                    {
                        if (kvp.Value >= insertIndex)
                        {
                            method.IndexById[kvp.Key] += insertedLines;
                        }
                    }

                    method.Code.InsertRange(insertIndex, cblock);

                    if (!string.IsNullOrEmpty(conditionId))
                    {
                        method.IndexById[conditionId] = insertIndex + insertedLines - 1;
                    }
                }
                else
                {
                    // Adjust indexes for non-conditional inserts too
                    int insertedLines = block.Code.Count;
                    foreach (var kvp in method.IndexById.ToList())
                    {
                        if (kvp.Value >= insertIndex)
                        {
                            method.IndexById[kvp.Key] += insertedLines;
                        }
                    }

                    method.Code.InsertRange(insertIndex, block.Code);
                }
            }
        }
    }
}
