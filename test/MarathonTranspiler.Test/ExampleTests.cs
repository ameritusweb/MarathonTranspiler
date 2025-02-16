using MarathonTranspiler.Core;
using MarathonTranspiler.Readers;

namespace MarathonTranspiler.Test
{
    [TestFixture]
    public class ExampleTests
    {
        [Test]
        public void Example1Test()
        {
            var rootDirectory = Directory.GetCurrentDirectory();
            var fullPath = Path.Combine(rootDirectory, "Examples\\example1.mrt");
            var marathonReader = new MarathonReader();
            var annotatedCode = marathonReader.ReadFile(fullPath);

            Config config = new Config();
            config.TranspilerOptions = new TranspilerOptions();
            config.TranspilerOptions.Target = "csharp";
            config.TranspilerOptions.CSharp = new Transpilers.CSharp.CSharpConfig()
            {
                TestFramework = "nunit",
            };

            var transpiler = TranspilerFactory.CreateTranspiler(config.TranspilerOptions);
            transpiler.ProcessAnnotatedCode(annotatedCode);
            var output = transpiler.GenerateOutput();
        }

        [Test]
        public void Example2Test()
        {
            var rootDirectory = Directory.GetCurrentDirectory();
            var fullPath = Path.Combine(rootDirectory, "Examples\\example2.mrt");
            var marathonReader = new MarathonReader();
            var annotatedCode = marathonReader.ReadFile(fullPath);

            Config config = new Config();
            config.TranspilerOptions = new TranspilerOptions();
            config.TranspilerOptions.Target = "orleans";
            config.TranspilerOptions.Orleans = new Transpilers.Orleans.OrleansConfig()
            {
                Stateful = true,
                GrainKeyTypes = new Dictionary<string, string>() {
                    { "NodeGrain", "Guid" },
                    { "AgentGrain", "Guid" },
                },
            };

            var transpiler = TranspilerFactory.CreateTranspiler(config.TranspilerOptions);
            transpiler.ProcessAnnotatedCode(annotatedCode);
            var output = transpiler.GenerateOutput();
        }
    }
}
