using MarathonTranspiler.Core;
using MarathonTranspiler.Transpilers.CSharp;

namespace MarathonTranspiler.Test
{
    [TestFixture]
    public class CSharpTranspilerTests
    {
        private CSharpTranspiler _transpiler;
        private List<AnnotatedCode> _annotatedCode;

        [SetUp]
        public void Setup()
        {
            _transpiler = new CSharpTranspiler(new CSharpConfig() { 
                TestFramework = ".net8.0"
            });
            _annotatedCode = new List<AnnotatedCode>();
        }

        [Test]
        public void BasicClassGeneration_ShouldCreateValidClass()
        {
            // Arrange
            var code = new AnnotatedCode
            {
                Annotations = new List<Annotation>
                {
                    new Annotation
                    {
                        Name = "varInit",
                        Values = new List<KeyValuePair<string, string>>
                        {
                            new("className", "Calculator"),
                            new("type", "float")
                        }
                    }
                },
                Code = new List<string> { "this.Value = 0f;" }
            };
            _annotatedCode.Add(code);

            // Act
            _transpiler.ProcessAnnotatedCode(_annotatedCode);
            var output = _transpiler.GenerateOutput();

            // Assert
            StringAssert.Contains("public class Calculator", output);
            StringAssert.Contains("public float Value { get; set; }", output);
        }
    }
}