using Acornima.Ast;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Extensions
{
    public static class StringExtensions
    {
        public static string NoLineNumber(this string str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return str;
            }
            else
            {
                int index = str.IndexOf(":");
                if (index == -1)
                {
                    return str;
                }

                return str.Substring(index + 1);
            }
        }
    }
}
