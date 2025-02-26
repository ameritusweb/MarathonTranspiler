using MarathonTranspiler.Core;
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
        protected override void ProcessVarInit(TranspiledClass currentClass, AnnotatedCode block)
        {
            // Original functionality for class-level variable initialization
            var annotation = block.Annotations[0];
            var type = annotation.Values.First(v => v.Key == "type").Value;

            // Check if it's a local variable declaration (inside a method)
            var isLocalVar = block.Code.Any(line => line.Trim().StartsWith("var "));

            if (isLocalVar)
            {
                // This is a local variable declaration, not a class property
                // Nothing special needed here as the code line is already included in the method's body
                return;
            }

            if (!block.Code[0].StartsWith("this."))
            {
                currentClass.Fields.Add(block.Code[0]);
            }
            else
            {
                var propertyName = block.Code[0].Split('=')[0].Replace("this.", "").Trim();
                currentClass.Properties.Add(new TranspiledProperty { Name = propertyName, Type = type });
                currentClass.ConstructorLines.Add(block.Code[0]);
            }
        }
    }
}
