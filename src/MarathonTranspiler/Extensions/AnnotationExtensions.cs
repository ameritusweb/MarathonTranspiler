using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Extensions
{
    public static class AnnotationExtensions
    {
        public static string GetValue(this List<KeyValuePair<string, string>> values, string key, string defaultValue = null)
        {
            return values.FirstOrDefault(v => v.Key == key).Value ?? defaultValue;
        }
    }
}
