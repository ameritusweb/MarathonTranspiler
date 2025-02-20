using MarathonTranspiler.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.Orleans
{
    public partial class OrleansTranspiler : MarathonTranspilerBase
    {
        protected override void ProcessInject(TranspiledClass currentClass, AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var type = annotation.Values.First(v => v.Key == "type").Value;
            var name = annotation.Values.First(v => v.Key == "name").Value;

            currentClass.Injections.Add(new InjectedDependency
            {
                Type = type,
                Name = name
            });
            currentClass.Fields.Add($"private readonly {type} {name};");
        }
    }
}
