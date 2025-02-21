using MarathonTranspiler.Core;
using MarathonTranspiler.Model.React;
using MarathonTranspiler.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.ReactRedux
{
    public class ReactReduxTranspiler : MarathonTranspilerBase
    {
        private readonly HashSet<string> _imports = new()
        {
            "import React from 'react';",
            "import { useSelector, useDispatch } from 'react-redux';",
            "import { Provider } from 'react-redux';",
            "import { createSlice, createAsyncThunk, configureStore } from '@reduxjs/toolkit';"
        };

        private readonly ReactReduxConfig _config;

        public ReactReduxTranspiler(ReactReduxConfig config)
        {
            this._config = config;
        }

        protected override void ProcessBlock(AnnotatedCode block, AnnotatedCode? previousBlock)
        {
            var mainAnnotation = block.Annotations[0];

            switch (mainAnnotation.Name)
            {
                case "xml":
                    if (mainAnnotation.Values.Any(v => v.Key == "pageName"))
                    {
                        var pageName = mainAnnotation.Values.First(v => v.Key == "pageName").Value;
                        var currentPage = GetOrCreatePage(pageName);
                        ProcessXml(currentPage, block);
                    }
                    else if (mainAnnotation.Values.Any(v => v.Key == "componentName"))
                    {
                        var componentName = mainAnnotation.Values.First(v => v.Key == "componentName").Value;
                        var currentComponent = GetOrCreateComponent(componentName);
                        ProcessXml(currentComponent, block);
                    }
                    break;

                case "varInit":
                case "run":
                    ProcessReduxAnnotation(block);
                    break;

                case "domInit":
                    ProcessDomInit(block);
                    break;

                case "assert":
                    ProcessAssertion(block);
                    break;
            }
        }

        private void ProcessReduxAnnotation(AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var className = annotation.Values.First(v => v.Key == "className").Value;
            var component = Components[className] as ReactComponent;

            if (component.ReduxSlice == null)
            {
                component.ReduxSlice = new StoreSlice { Name = className.ToLower() };
            }

            if (annotation.Name == "varInit")
            {
                var type = annotation.Values.First(v => v.Key == "type").Value;
                var varName = block.Code[0].Split('=')[0].Replace("this.", "").Trim();
                var initialValue = block.Code[0].Split('=')[1].Trim();
                component.ReduxSlice.InitialState[varName] = initialValue;
            }
            else if (annotation.Name == "run")
            {
                var functionName = annotation.Values.First(v => v.Key == "functionName").Value;
                var isAsync = annotation.Values.Any(v => v.Key == "isAsync" && v.Value == "true");

                if (isAsync)
                {
                    ProcessAsyncAction(component, functionName, block);
                }
                else
                {
                    ProcessSyncAction(component, functionName, block);
                }
            }
        }

        private void ProcessAsyncAction(ReactComponent component, string functionName, AnnotatedCode block)
        {
            var thunk = new AsyncThunk
            {
                Name = functionName,
                Parameters = ExtractParameters(block.Code),
                ApiCall = block.Code.First(line => line.Contains("await")),
                AdditionalCode = block.Code.Where(line => !line.Contains("await")).ToList()
            };

            component.ReduxSlice.AsyncThunks.Add(thunk);
        }

        private void ProcessSyncAction(ReactComponent component, string functionName, AnnotatedCode block)
        {
            var action = new ReduxAction
            {
                Name = functionName,
                Parameters = ExtractParameters(block.Code),
                Code = block.Code.Select(line => line.Replace("this.", "state.")).ToList()
            };

            component.ReduxSlice.Actions.Add(action);
        }

        private void ProcessDomInit(AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var target = annotation.Values.First(v => v.Key == "target").Value;
            var component = Components.Values.OfType<ReactComponent>()
                .FirstOrDefault(c => c.DomAttributes.ContainsKey(target));

            if (component != null)
            {
                foreach (var attr in annotation.Values.Where(v =>
                    v.Key != "target" && v.Key != "tag" && v.Key != "class"))
                {
                    component.DomAttributes[attr.Key] = attr.Value;
                }
            }
        }

        private void ProcessAssertion(AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var className = annotation.Values.First(v => v.Key == "className").Value;
            var component = Components[className] as ReactComponent;

            component.Assertions.Add(new Assertion
            {
                ClassName = className,
                Condition = annotation.Values.First(v => v.Key == "condition").Value,
                Message = block.Code[0].Trim('"'),
                Action = annotation.Values.FirstOrDefault(v => v.Key == "action").Value
            });
        }

        public override string GenerateOutput()
        {
            // Generate Redux store
            GenerateStore();

            // Generate components
            foreach (var component in Components.Values.OfType<ReactComponent>())
            {
                GenerateComponent(component);
            }

            // Generate pages
            GeneratePages();

            // Generate tests
            GenerateTests();

            // Generate routes
            GenerateRoutes();

            return string.Empty; // Files are written directly
        }

        private void GenerateStore()
        {
            var sb = new StringBuilder();

            // Add imports
            foreach (var import in _imports)
            {
                sb.AppendLine(import);
            }
            sb.AppendLine();

            // Generate slices
            foreach (var component in Components.Values.OfType<ReactComponent>()
                .Where(c => c.ReduxSlice != null))
            {
                GenerateReduxSlice(sb, component);
            }

            // Generate store configuration
            sb.AppendLine("export const store = configureStore({");
            sb.AppendLine("  reducer: {");
            foreach (var component in Components.Values.OfType<ReactComponent>()
                .Where(c => c.ReduxSlice != null))
            {
                sb.AppendLine($"    {component.ReduxSlice.Name}: {component.ReduxSlice.Name}Reducer,");
            }
            sb.AppendLine("  },");
            sb.AppendLine("});");

            File.WriteAllText("store.js", sb.ToString());
        }

        private void GenerateReduxSlice(StringBuilder sb, ReactComponent component)
        {
            var slice = component.ReduxSlice;

            // Generate async thunks first
            foreach (var thunk in slice.AsyncThunks)
            {
                sb.AppendLine($"export const {thunk.Name} = createAsyncThunk(");
                sb.AppendLine($"  '{slice.Name}/{thunk.Name}',");
                sb.AppendLine($"  async ({string.Join(", ", thunk.Parameters)}) => {{");
                sb.AppendLine($"    {thunk.ApiCall}");
                foreach (var line in thunk.AdditionalCode)
                {
                    sb.AppendLine($"    {line}");
                }
                sb.AppendLine("  }");
                sb.AppendLine(");");
                sb.AppendLine();
            }

            // Generate slice
            sb.AppendLine($"const {slice.Name}Slice = createSlice({{");
            sb.AppendLine($"  name: '{slice.Name}',");

            // Initial state
            sb.AppendLine("  initialState: {");
            foreach (var state in slice.InitialState)
            {
                sb.AppendLine($"    {state.Key}: {state.Value},");
            }
            sb.AppendLine("  },");

            // Reducers
            sb.AppendLine("  reducers: {");
            foreach (var action in slice.Actions)
            {
                sb.AppendLine($"    {action.Name}: (state, action) => {{");
                foreach (var line in action.Code)
                {
                    sb.AppendLine($"      {line}");
                }
                sb.AppendLine("    },");
            }
            sb.AppendLine("  },");

            // Extra reducers for async thunks
            if (slice.AsyncThunks.Any())
            {
                sb.AppendLine("  extraReducers: (builder) => {");
                foreach (var thunk in slice.AsyncThunks)
                {
                    sb.AppendLine($"    builder.addCase({thunk.Name}.pending, (state) => {{");
                    sb.AppendLine($"      state.loading = true;");
                    sb.AppendLine("    })");
                    sb.AppendLine($"    .addCase({thunk.Name}.fulfilled, (state, action) => {{");
                    sb.AppendLine($"      state.loading = false;");
                    sb.AppendLine($"      state.{thunk.StateProperty} = action.payload;");
                    sb.AppendLine("    })");
                    sb.AppendLine($"    .addCase({thunk.Name}.rejected, (state, action) => {{");
                    sb.AppendLine($"      state.loading = false;");
                    sb.AppendLine($"      state.error = action.error.message;");
                    sb.AppendLine("    });");
                }
                sb.AppendLine("  },");
            }

            sb.AppendLine("});");
            sb.AppendLine();

            // Export actions and reducer
            var actions = string.Join(", ", slice.Actions.Select(a => a.Name));
            sb.AppendLine($"export const {{ {actions} }} = {slice.Name}Slice.actions;");
            sb.AppendLine($"export const {slice.Name}Reducer = {slice.Name}Slice.reducer;");
            sb.AppendLine();
        }

        private void GenerateComponent(ReactComponent component)
        {
            var sb = new StringBuilder();

            // Add imports
            foreach (var import in component.RequiredImports)
            {
                sb.AppendLine($"import {import} from './{import}';");
            }
            if (component.ReduxSlice != null)
            {
                sb.AppendLine("import { useSelector, useDispatch } from 'react-redux';");
                sb.AppendLine($"import {{ {string.Join(", ", component.ReduxSlice.Actions.Select(a => a.Name))} }} from './store';");
            }
            sb.AppendLine();

            // Generate component
            var props = string.Join(", ", component.Props);
            sb.AppendLine($"function {component.Name}({{ {props} }}) {{");

            // Redux hooks
            if (component.ReduxSlice != null)
            {
                sb.AppendLine("  const dispatch = useDispatch();");
                foreach (var state in component.ReduxSlice.InitialState.Keys)
                {
                    sb.AppendLine($"  const {state} = useSelector(state => state.{component.ReduxSlice.Name}.{state});");
                }
            }

            // Return JSX
            sb.AppendLine("  return (");
            GenerateJSX(sb, component, 2);
            sb.AppendLine("  );");
            sb.AppendLine("}");
            sb.AppendLine();

            // Export
            if (component.ReduxSlice != null)
            {
                sb.AppendLine("export default connect(");
                sb.AppendLine("  state => ({");
                foreach (var state in component.ReduxSlice.InitialState.Keys)
                {
                    sb.AppendLine($"    {state}: state.{component.ReduxSlice.Name}.{state},");
                }
                sb.AppendLine("  }),");
                sb.AppendLine("  {");
                foreach (var action in component.ReduxSlice.Actions)
                {
                    sb.AppendLine($"    {action.Name},");
                }
                sb.AppendLine("  }");
                sb.AppendLine($")({component.Name});");
            }
            else
            {
                sb.AppendLine($"export default {component.Name};");
            }

            File.WriteAllText($"{component.Name}.jsx", sb.ToString());
        }

        private void GenerateJSX(StringBuilder sb, ReactComponent component, int indent)
        {
            var spaces = new string(' ', indent * 2);
            sb.AppendLine($"{spaces}<div className=\"{component.DomAttributes.GetValueOrDefault("className")}\">");

            foreach (var child in component.Children)
            {
                GenerateJSX(sb, child as ReactComponent, indent + 1);
            }

            sb.AppendLine($"{spaces}</div>");
        }

        private void GenerateTests()
        {
            var sb = new StringBuilder();

            sb.AppendLine("import { render, screen } from '@testing-library/react';");
            sb.AppendLine("import { Provider } from 'react-redux';");
            sb.AppendLine("import { store } from './store';");
            sb.AppendLine();

            foreach (var component in Components.Values.OfType<ReactComponent>()
                .Where(c => c.Assertions.Any()))
            {
                sb.AppendLine($"import {component.Name} from './{component.Name}';");
            }
            sb.AppendLine();

            foreach (var component in Components.Values.OfType<ReactComponent>()
                .Where(c => c.Assertions.Any()))
            {
                sb.AppendLine($"describe('{component.Name}', () => {{");

                foreach (var assertion in component.Assertions)
                {
                    sb.AppendLine($"  test('{assertion.Message}', () => {{");
                    if (component.ReduxSlice != null)
                    {
                        sb.AppendLine("    render(");
                        sb.AppendLine("      <Provider store={store}>");
                        sb.AppendLine($"        <{component.Name} />");
                        sb.AppendLine("      </Provider>");
                        sb.AppendLine("    );");
                    }
                    else
                    {
                        sb.AppendLine($"    render(<{component.Name} />);");
                    }
                    sb.AppendLine($"    expect({assertion.Condition}).toBeTruthy();");
                    sb.AppendLine("  });");
                }

                sb.AppendLine("});");
                sb.AppendLine();
            }

            File.WriteAllText("tests.js", sb.ToString());
        }

        protected override void ProcessXml(TranspiledPage currentPage, AnnotatedCode block)
        {
            var reactPage = (currentPage as ReactPage) ?? new ReactPage
            {
                Name = currentPage.Name,
                Route = currentPage.Route
            };

            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(string.Join("\n", block.Code));

            foreach (System.Xml.XmlNode pageNode in doc.SelectNodes("//Page"))
            {
                // Process page layout
                foreach (System.Xml.XmlNode layoutNode in pageNode.SelectNodes(".//*"))
                {
                    var componentName = layoutNode.Name;
                    reactPage.LayoutComponents.Add(componentName);

                    // Add to required imports
                    reactPage.RequiredImports.Add(componentName);
                }

                // Process page metadata
                if (pageNode.Attributes != null)
                {
                    foreach (System.Xml.XmlAttribute attr in pageNode.Attributes)
                    {
                        reactPage.PageMetadata[attr.Name] = attr.Value;
                    }
                }
            }

            Pages[reactPage.Route] = reactPage;
        }

        protected override void ProcessXml(TranspiledComponent currentComponent, AnnotatedCode block)
        {
            var reactComponent = (currentComponent as ReactComponent) ?? new ReactComponent
            {
                Name = currentComponent.Name
            };

            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(string.Join("\n", block.Code));

            foreach (System.Xml.XmlNode componentNode in doc.SelectNodes("//Component"))
            {
                // Process props
                foreach (System.Xml.XmlNode propNode in componentNode.SelectNodes(".//Prop"))
                {
                    var propName = propNode.Attributes["name"].Value;
                    reactComponent.Props.Add(propName);

                    // Check for Redux state props
                    if (propNode.Attributes["reduxState"]?.Value == "true")
                    {
                        if (reactComponent.ReduxSlice == null)
                        {
                            reactComponent.ReduxSlice = new StoreSlice
                            {
                                Name = reactComponent.Name.ToLower()
                            };
                        }
                        reactComponent.ReduxSlice.InitialState[propName] =
                            propNode.Attributes["default"]?.Value ?? "null";
                    }
                }

                // Process child components
                foreach (System.Xml.XmlNode childNode in componentNode.ChildNodes)
                {
                    if (childNode.NodeType == System.Xml.XmlNodeType.Element &&
                        childNode.Name != "Prop")
                    {
                        var childComponent = new ReactComponent
                        {
                            Name = childNode.Name,
                            DomAttributes = new Dictionary<string, string>()
                        };

                        // Copy any attributes from the XML to the component
                        foreach (System.Xml.XmlAttribute attr in childNode.Attributes)
                        {
                            childComponent.DomAttributes[attr.Name] = attr.Value;
                        }

                        reactComponent.Children.Add(childComponent);
                        reactComponent.RequiredImports.Add(childNode.Name);
                    }
                }
            }

            Components[reactComponent.Name] = reactComponent;
        }

        private void GeneratePages()
        {
            foreach (ReactPage page in Pages.Values)
            {
                var sb = new StringBuilder();

                // Add imports
                foreach (var import in page.RequiredImports)
                {
                    sb.AppendLine($"import {import} from '../components/{import}';");
                }

                // Generate page component
                sb.AppendLine($"function {page.Name}() {{");
                sb.AppendLine("  return (");
                sb.AppendLine("    <div>");
                foreach (var component in page.LayoutComponents)
                {
                    sb.AppendLine($"      <{component} />");
                }
                sb.AppendLine("    </div>");
                sb.AppendLine("  );");
                sb.AppendLine("}");

                // Export
                sb.AppendLine($"export default {page.Name};");

                // Save page file
                File.WriteAllText($"{page.Name}.jsx", sb.ToString());
            }
        }

        private void GenerateRoutes()
        {
            var sb = new StringBuilder();

            sb.AppendLine("import { Routes, Route } from 'react-router-dom';");
            foreach (var page in Pages.Values.OfType<ReactPage>())
            {
                sb.AppendLine($"import {page.Name} from './{page.Name}';");
            }
            sb.AppendLine();

            sb.AppendLine("export default function AppRoutes() {");
            sb.AppendLine("  return (");
            sb.AppendLine("    <Routes>");
            foreach (var page in Pages.Values.OfType<ReactPage>())
            {
                sb.AppendLine($"      <Route path=\"{page.Route}\" element={{<{page.Name} />}} />");
            }
            sb.AppendLine("    </Routes>");
            sb.AppendLine("  );");
            sb.AppendLine("}");

            File.WriteAllText("AppRoutes.jsx", sb.ToString());
        }

        private List<string> ExtractParameters(List<string> code)
        {
            var parameters = new List<string>();
            foreach (var line in code)
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(line, @"\$(\w+)");
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var param = match.Groups[1].Value;
                    if (!parameters.Contains(param))
                    {
                        parameters.Add(param);
                    }
                }
            }
            return parameters;
        }
    }
}
