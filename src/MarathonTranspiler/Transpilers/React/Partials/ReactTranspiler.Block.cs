using MarathonTranspiler.Core;
using MarathonTranspiler.Extensions;
using MarathonTranspiler.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.React
{
    public partial class ReactTranspiler : MarathonTranspilerBase
    {
        private readonly StaticMethodInliningHelper _inliningHelper;

        protected internal override void ProcessBlock(AnnotatedCode block, AnnotatedCode? previousBlock)
        {
            // Apply inlining before standard processing
            _inliningHelper.ProcessInlining(block);

            // Continue with normal processing
            base.ProcessBlock(block, previousBlock);
        }
    }
}
