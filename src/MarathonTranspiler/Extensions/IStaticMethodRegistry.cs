using MarathonTranspiler.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Extensions
{
    public interface IStaticMethodRegistry
    {
        bool TryGetMethod(string language, string className, string methodName, out MethodInfo? method);
    }
}
