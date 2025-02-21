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
            if (!block.Code[0].StartsWith("this."))
            {
                currentClass.Fields.Add(block.Code[0]);
            }
            else
            {
                var type = block.Annotations[0].Values.First(v => v.Key == "type").Value;
                var propertyName = block.Code[0].Split('=')[0].Replace("this.", "").Trim();
                currentClass.Properties.Add(new TranspiledProperty { Name = propertyName, Type = type });
                currentClass.ConstructorLines.Add(block.Code[0]);
            }
        }
    }
}
