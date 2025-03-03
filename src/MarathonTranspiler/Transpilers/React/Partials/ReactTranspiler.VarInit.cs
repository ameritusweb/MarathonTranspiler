using MarathonTranspiler.Core;
using MarathonTranspiler.Extensions;
using MarathonTranspiler.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.React
{
    public partial class ReactTranspiler : MarathonTranspilerBase
    {
        protected override void ProcessVarInit(TranspiledClass currentClass, AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var varName = block.Code[0].NoLineNumber().Split('=')[0].Replace("this.", "").Trim();
            var initialValue = block.Code[0].NoLineNumber().Split('=')[1].Trim();

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
    }
}
