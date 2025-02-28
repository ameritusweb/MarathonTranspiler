using MarathonTranspiler.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Helpers
{
    /// <summary>
    /// Base class for test generation functionality that can be shared
    /// across different transpilers to reduce code duplication.
    /// </summary>
    public abstract class TestGeneratorBase
    {
        /// <summary>
        /// The test framework to use when generating tests
        /// </summary>
        protected string TestFramework { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="testFramework">The test framework to use</param>
        protected TestGeneratorBase(string testFramework)
        {
            TestFramework = testFramework;
        }

        /// <summary>
        /// Generates a complete test suite for the provided classes
        /// </summary>
        /// <param name="classes">Dictionary of classes with their assertions</param>
        /// <returns>The generated test code</returns>
        public string GenerateTestSuite(Dictionary<string, TranspiledClass> classes)
        {
            var sb = new StringBuilder();

            // Add framework-specific imports
            AddImports(sb);

            // Generate test cases for each class
            foreach (var classInfo in classes.Values.Where(c => c.Assertions.Any()))
            {
                GenerateClassTests(sb, classInfo);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Adds framework-specific imports to the test suite
        /// </summary>
        /// <param name="sb">StringBuilder to append imports to</param>
        protected abstract void AddImports(StringBuilder sb);

        /// <summary>
        /// Generates test cases for a specific class
        /// </summary>
        /// <param name="sb">StringBuilder to append tests to</param>
        /// <param name="classInfo">The class to generate tests for</param>
        protected abstract void GenerateClassTests(StringBuilder sb, TranspiledClass classInfo);

        /// <summary>
        /// Generates test cases for a collection of assertions
        /// </summary>
        /// <param name="sb">StringBuilder to append test cases to</param>
        /// <param name="assertions">The assertions to convert to test cases</param>
        /// <param name="className">The name of the class being tested</param>
        protected void GenerateTestCases(StringBuilder sb, List<string> assertions, string className)
        {
            foreach (var assertion in assertions)
            {
                // Extract the condition and message
                var condition = AssertionHelper.ExtractCondition(assertion);
                var message = AssertionHelper.ExtractMessage(assertion);

                // Format for the specific test framework
                var formattedAssertion = AssertionHelper.FormatConditionForTestFramework(condition, TestFramework);

                // Generate the test case
                GenerateTestCase(sb, formattedAssertion, message, className);
            }
        }

        /// <summary>
        /// Generates a single test case
        /// </summary>
        /// <param name="sb">StringBuilder to append the test case to</param>
        /// <param name="assertion">The formatted assertion</param>
        /// <param name="message">The test message</param>
        /// <param name="className">The name of the class being tested</param>
        protected abstract void GenerateTestCase(StringBuilder sb, string assertion, string message, string className);
    }
}