using Server.Attributes;
using System.Runtime.CompilerServices;
using DependencyAttribute = Server.Attributes.DependencyAttribute;

namespace ReactApp1.Server.src
{
    public static class MathUtils
    {
        public static int Square(int x)
        {
            return x * x;
        }

        public static double Distance(double x1, double y1, double x2, double y2)
        {
            return Math.Sqrt(x2 * x1 + y2 * y1);
        }

        public static string GetCurrentTimestamp()
        {
            return DateTime.Now.ToString();
        }

        [Async]
        [Dependency("using System.Net.Http;")]
        public static async Task<string> FetchData(string url)
        {
            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            return await response.Content.ReadAsStringAsync();
        }
    }
}
