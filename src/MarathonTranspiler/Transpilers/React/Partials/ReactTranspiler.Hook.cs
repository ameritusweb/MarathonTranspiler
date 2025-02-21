using MarathonTranspiler.Core;
using MarathonTranspiler.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.React
{
    public partial class ReactTranspiler : MarathonTranspilerBase
    {
        protected override void ProcessHook(TranspiledClass currentClass, AnnotatedCode block)
        {
            var mainAnnotation = block.Annotations[0];
            var hookName = mainAnnotation.Values.First(v => v.Key == "name").Value;
            _customHooks[hookName] = block.Code;
        }
    }
}
