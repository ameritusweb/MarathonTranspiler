using MarathonTranspiler.Core;
using MarathonTranspiler.Helpers;
using MarathonTranspiler.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.CSharp
{
    public partial class CSharpTranspiler : MarathonTranspilerBase
    {
        protected override void ProcessAssert(TranspiledClass currentClass, AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var condition = annotation.Values.First(v => v.Key == "condition").Value;
            var message = block.Code[0].Trim('"');

            // Use the shared AssertionHelper to format the assertion for the correct framework
            string assertLine = _config.TestFramework.ToLower() switch
            {
                "nunit" => AssertionHelper.FormatConditionForTestFramework(condition, "nunit"),
                _ => AssertionHelper.FormatConditionForTestFramework(condition, "xunit")
            };

            // If the formatted assertion didn't include the message, add it
            if (!assertLine.Contains(message))
            {
                // Remove trailing semicolon if present
                if (assertLine.EndsWith(";"))
                {
                    assertLine = assertLine.Substring(0, assertLine.Length - 1);
                }

                // Add the message
                if (_config.TestFramework.ToLower() == "nunit")
                {
                    // NUnit format
                    assertLine = assertLine.Substring(0, assertLine.Length - 1) + $", \"{message}\");";
                }
                else
                {
                    // xUnit format
                    assertLine = assertLine.Substring(0, assertLine.Length - 1) + $", \"{message}\");";
                }
            }

            currentClass.Assertions.Add(assertLine);
        }
    }
}