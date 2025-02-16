using MarathonTranspiler.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.React
{
    public class ReactTranspiler : MarathonTranspilerBase
    {
        private readonly StringBuilder _jsxOutput = new();
        private readonly HashSet<string> _imports = new() { "import React from 'react';" };
        private readonly Dictionary<string, List<string>> _customHooks = new();

        protected override void ProcessBlock(AnnotatedCode block)
        {
            var mainAnnotation = block.Annotations[0];

            if (mainAnnotation.Name == "hook")
            {
                var hookName = mainAnnotation.Values.First(v => v.Key == "name").Value;
                _customHooks[hookName] = block.Code;
            }
            else
            {
                base.ProcessBlock(block);
            }
        }

        protected override void ProcessVarInit(TranspiledClass currentClass, AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var varName = block.Code[0].Split('=')[0].Replace("this.", "").Trim();
            var initialValue = block.Code[0].Split('=')[1].Trim();

            if (annotation.Values.Any(v => v.Key == "hookName"))
            {
                var hookName = annotation.Values.First(v => v.Key == "hookName").Value;

                if (_customHooks.ContainsKey(hookName))
                {
                    _mainMethodLines.Add($"const {varName} = {hookName}();");
                }
                else
                {
                    switch (hookName)
                    {
                        case "useState":
                            _mainMethodLines.Add($"const [{varName}, set{char.ToUpper(varName[0])}{varName.Substring(1)}] = useState({initialValue});");
                            _imports.Add("import { useState } from 'react';");
                            break;

                        case "useEffect":
                            _mainMethodLines.Add("useEffect(() => {");
                            _mainMethodLines.AddRange(block.Code.Select(line => $"    {line}"));
                            _mainMethodLines.Add("}, []);");
                            _imports.Add("import { useEffect } from 'react';");
                            break;

                        case "useCallback":
                            _mainMethodLines.Add($"const {varName} = useCallback({initialValue}, []);");
                            _imports.Add("import { useCallback } from 'react';");
                            break;

                        case "useMemo":
                            _mainMethodLines.Add($"const {varName} = useMemo(() => {initialValue}, []);");
                            _imports.Add("import { useMemo } from 'react';");
                            break;

                        case "useRef":
                            _mainMethodLines.Add($"const {varName} = useRef({initialValue});");
                            _imports.Add("import { useRef } from 'react';");
                            break;

                        case "useContext":
                            _mainMethodLines.Add($"const {varName} = useContext({initialValue});");
                            _imports.Add("import { useContext } from 'react';");
                            break;
                    }
                }
            }
            else
            {
                _mainMethodLines.Add($"const {varName} = {initialValue};");
            }
        }

        protected override void ProcessRun(TranspiledClass currentClass, AnnotatedCode block)
        {
            var functionName = block.Annotations[0].Values.First(v => v.Key == "functionName").Value;

            _mainMethodLines.Add($"const {functionName} = () => {{");
            _mainMethodLines.AddRange(block.Code.Select(line => $"    {line}"));
            _mainMethodLines.Add("};");
        }

        protected override void ProcessEvent(TranspiledClass currentClass, AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var eventName = annotation.Values.First(v => v.Key == "event").Value;
            var target = annotation.Values.First(v => v.Key == "target").Value;

            var handlerName = $"handle{target}{eventName}";
            _mainMethodLines.Add($"const {handlerName} = () => {{");
            _mainMethodLines.AddRange(block.Code.Select(line => $"    {line}"));
            _mainMethodLines.Add("};");

            _jsxOutput.AppendLine($"<button onClick={{{handlerName}}}>{target}</button>");
        }

        public override string GenerateOutput()
        {
            var sb = new StringBuilder();

            // Add imports
            foreach (var import in _imports)
            {
                sb.AppendLine(import);
            }
            sb.AppendLine();

            // Generate custom hooks
            foreach (var hook in _customHooks)
            {
                sb.AppendLine($"function {hook.Key}() {{");
                foreach (var line in hook.Value)
                {
                    sb.AppendLine($"    {line}");
                }
                sb.AppendLine("}");
                sb.AppendLine();
            }

            // Generate components
            foreach (var classInfo in _classes.Values)
            {
                sb.AppendLine($"function {classInfo.ClassName}() {{");

                // State and handlers
                foreach (var line in _mainMethodLines)
                {
                    sb.AppendLine($"    {line}");
                }
                sb.AppendLine();

                // JSX
                sb.AppendLine("    return (");
                sb.AppendLine("        <div>");
                sb.Append(_jsxOutput);
                sb.AppendLine("        </div>");
                sb.AppendLine("    );");
                sb.AppendLine("}");

                sb.AppendLine($"export default {classInfo.ClassName};");
            }

            return sb.ToString();
        }
    }
}
