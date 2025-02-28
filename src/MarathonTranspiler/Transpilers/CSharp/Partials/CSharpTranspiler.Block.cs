using MarathonTranspiler.Core;
using MarathonTranspiler.Extensions;
using MarathonTranspiler.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.CSharp
{
    public partial class CSharpTranspiler : MarathonTranspilerBase
    {
        private readonly StaticMethodInliningHelper _inliningHelper;

        protected internal override void ProcessBlock(AnnotatedCode block, AnnotatedCode? previousBlock)
        {
            // Apply inlining before standard processing
            _inliningHelper.ProcessInlining(block, "csharp");

            // Continue with normal processing
            base.ProcessBlock(block, previousBlock);
        }
    }
}
