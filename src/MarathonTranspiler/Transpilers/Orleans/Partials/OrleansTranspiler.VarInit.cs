using MarathonTranspiler.Core;
using MarathonTranspiler.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.Orleans
{
    public partial class OrleansTranspiler : MarathonTranspilerBase
    {
        protected override void ProcessVarInit(TranspiledClass currentClass, AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var type = annotation.Values.First(v => v.Key == "type").Value;
            var propertyName = block.Code[0].Split('=')[0].Replace("this.", "").Trim();
            var initialValue = block.Code[0].Split('=')[1].Trim().Replace(";", "");

            if (annotation.Values.Any(v => v.Key == "stateName"))
            {
                var stateName = annotation.Values.First(v => v.Key == "stateName").Value;
                var stateId = annotation.Values.FirstOrDefault(v => v.Key == "stateId").Value;

                currentClass.Properties.Add(new TranspiledProperty
                {
                    Name = propertyName,
                    Type = type,
                    StateName = stateName,
                    StateId = stateId,
                    Code = !block.Code[0].StartsWith("this.") ? block.Code[0].Trim() : null,
                });

                if (block.Code[0].StartsWith("this."))
                {
                    // Add initialization to constructor
                    currentClass.ConstructorLines.Add($"this.{propertyName} = {initialValue};");
                }
            }
            else if (!block.Code[0].StartsWith("this."))
            {
                currentClass.Fields.Add(block.Code[0]);
            }
            else
            {
                currentClass.Properties.Add(new TranspiledProperty
                {
                    Name = propertyName,
                    Type = type
                });
                currentClass.ConstructorLines.Add(block.Code[0]);
            }
        }
    }
}
