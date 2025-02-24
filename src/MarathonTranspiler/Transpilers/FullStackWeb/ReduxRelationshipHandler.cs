using MarathonTranspiler.Core;
using MarathonTranspiler.Extensions;
using MarathonTranspiler.Model.React;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.FullStackWeb
{
    public class ReduxRelationshipHandler
    {
        private readonly Dictionary<string, RelatedState> _relatedStates = new();

        public class RelatedState
        {
            public string Name { get; set; }
            public List<string> RelatedEntities { get; set; } = new();
            public List<AsyncOperation> AsyncOperations { get; set; } = new();
        }

        public class AsyncOperation
        {
            public string Name { get; set; }
            public List<string> Parameters { get; set; } = new();
            public List<string> EntitiesAffected { get; set; } = new();
            public List<string> Code { get; set; } = new();
        }

        public void ProcessReduxState(AnnotatedCode block)
        {
            var className = block.Annotations[0].Values.GetValue("className");
            var stateName = block.Code[0].Split('=')[0].Replace("this.", "").Trim();

            if (!_relatedStates.ContainsKey(className))
            {
                _relatedStates[className] = new RelatedState { Name = className };
            }

            _relatedStates[className].RelatedEntities.Add(stateName);
        }

        public void ProcessAsyncOperation(AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var className = annotation.Values.GetValue("className");
            var functionName = annotation.Values.GetValue("functionName");

            var operation = new AsyncOperation
            {
                Name = functionName,
                Code = block.Code,
                Parameters = block.Annotations
                    .Where(a => a.Name == "parameter")
                    .Select(a => a.Values.GetValue("name"))
                    .ToList(),
                EntitiesAffected = ExtractAffectedEntities(block.Code)
            };

            _relatedStates[className].AsyncOperations.Add(operation);
        }

        private List<string> ExtractAffectedEntities(List<string> code)
        {
            return code
                .Where(line => line.StartsWith("this."))
                .Select(line => line.Split('=')[0].Replace("this.", "").Trim())
                .Distinct()
                .ToList();
        }

        public void GenerateReduxSlice(StringBuilder sb, string storeName)
        {
            var state = _relatedStates[storeName];

            // Generate initial state
            sb.AppendLine("const initialState = {");
            foreach (var entity in state.RelatedEntities)
            {
                var defaultValue = entity.EndsWith("s") ? "[]" : "{}";
                sb.AppendLine($"  {entity}: {defaultValue},");
            }
            sb.AppendLine("};");
            sb.AppendLine();

            // Generate selectors
            foreach (var entity in state.RelatedEntities)
            {
                sb.AppendLine($"export const select{char.ToUpper(entity[0])}{entity.Substring(1)} = ");
                sb.AppendLine($"  state => state.{storeName.ToLower()}.{entity};");
            }
            sb.AppendLine();

            // Generate derived selectors for relationships
            GenerateDerivedSelectors(sb, state);

            // Generate async thunks
            foreach (var operation in state.AsyncOperations)
            {
                GenerateAsyncThunk(sb, operation, storeName);
            }

            // Generate slice
            sb.AppendLine($"const {storeName.ToLower()}Slice = createSlice({{");
            sb.AppendLine($"  name: '{storeName.ToLower()}',");
            sb.AppendLine("  initialState,");
            sb.AppendLine("  reducers: {},");
            sb.AppendLine("  extraReducers: (builder) => {");

            // Handle async operations
            foreach (var operation in state.AsyncOperations)
            {
                sb.AppendLine($"    builder");
                sb.AppendLine($"      .addCase({operation.Name}.pending, (state) => {{");
                foreach (var entity in operation.EntitiesAffected)
                {
                    sb.AppendLine($"        state.loadingStates.{entity} = true;");
                }
                sb.AppendLine("      })");
                sb.AppendLine($"      .addCase({operation.Name}.fulfilled, (state, action) => {{");
                foreach (var entity in operation.EntitiesAffected)
                {
                    sb.AppendLine($"        state.loadingStates.{entity} = false;");
                    sb.AppendLine($"        state.{entity} = action.payload.{entity};");
                }
                sb.AppendLine("      })");
                sb.AppendLine($"      .addCase({operation.Name}.rejected, (state, action) => {{");
                foreach (var entity in operation.EntitiesAffected)
                {
                    sb.AppendLine($"        state.loadingStates.{entity} = false;");
                    sb.AppendLine($"        state.error = action.error.message;");
                }
                sb.AppendLine("      });");
            }

            sb.AppendLine("  }");
            sb.AppendLine("});");
        }

        private void GenerateDerivedSelectors(StringBuilder sb, RelatedState state)
        {
            if (state.RelatedEntities.Contains("todos") &&
                state.RelatedEntities.Contains("categories"))
            {
                sb.AppendLine("export const selectTodosByCategory = createSelector(");
                sb.AppendLine("  [selectTodos, selectCategories, (_, categoryId) => categoryId],");
                sb.AppendLine("  (todos, categories, categoryId) => {");
                sb.AppendLine("    return todos.filter(todo => todo.categoryId === categoryId);");
                sb.AppendLine("  }");
                sb.AppendLine(");");
                sb.AppendLine();
            }

            if (state.RelatedEntities.Contains("todos") &&
                state.RelatedEntities.Contains("tags"))
            {
                sb.AppendLine("export const selectTodosByTag = createSelector(");
                sb.AppendLine("  [selectTodos, selectTags, (_, tagId) => tagId],");
                sb.AppendLine("  (todos, tags, tagId) => {");
                sb.AppendLine("    return todos.filter(todo => todo.tagIds.includes(tagId));");
                sb.AppendLine("  }");
                sb.AppendLine(");");
                sb.AppendLine();
            }
        }

        private void GenerateAsyncThunk(StringBuilder sb, AsyncOperation operation, string storeName)
        {
            var paramList = string.Join(", ", operation.Parameters);
            sb.AppendLine($"export const {operation.Name} = createAsyncThunk(");
            sb.AppendLine($"  '{storeName.ToLower()}/{operation.Name}',");
            sb.AppendLine($"  async ({paramList}) => {{");
            foreach (var line in operation.Code)
            {
                if (!line.StartsWith("this."))
                {
                    sb.AppendLine($"    {line}");
                }
            }
            sb.AppendLine("  }");
            sb.AppendLine(");");
            sb.AppendLine();
        }

        public void GenerateComponent(StringBuilder sb, string componentName, ReactComponent component)
        {
            // Component imports
            sb.AppendLine("import React from 'react';");
            sb.AppendLine("import { useSelector, useDispatch } from 'react-redux';");
            sb.AppendLine();

            // Generate component
            sb.AppendLine($"const {componentName} = () => {{");

            // Redux hooks
            sb.AppendLine("  const dispatch = useDispatch();");
            foreach (var prop in component.Props.Where(p => p.Contains("reduxState")))
            {
                var name = prop.Split(' ')[0];
                sb.AppendLine($"  const {name} = useSelector(select{char.ToUpper(name[0])}{name.Substring(1)});");
            }
            sb.AppendLine();

            // Loading states
            sb.AppendLine("  const { loadingStates } = useSelector(state => state);");
            sb.AppendLine();

            // Effects for data loading
            sb.AppendLine("  React.useEffect(() => {");
            sb.AppendLine("    dispatch(fetchTodosWithRelations());");
            sb.AppendLine("  }, [dispatch]);");
            sb.AppendLine();

            // Render JSX
            GenerateJSX(sb, component);

            sb.AppendLine("};");
            sb.AppendLine();
            sb.AppendLine($"export default {componentName};");
        }

        private void GenerateJSX(StringBuilder sb, ReactComponent component)
        {
            sb.AppendLine("  return (");
            sb.AppendLine("    <div className=\"space-y-4\">");

            // Loading states
            sb.AppendLine("      {loadingStates.todos && (");
            sb.AppendLine("        <div>Loading...</div>");
            sb.AppendLine("      )}");

            // Category filter
            if (component.Children.Any(c => c.Name == "CategoryFilter"))
            {
                sb.AppendLine("      <div className=\"flex gap-2\">");
                sb.AppendLine("        {categories.map(category => (");
                sb.AppendLine("          <button");
                sb.AppendLine("            key={category.id}");
                sb.AppendLine("            onClick={() => dispatch(filterByCategory(category.id))}");
                sb.AppendLine("            className={`px-4 py-2 rounded ${");
                sb.AppendLine("              selectedCategory === category.id");
                sb.AppendLine("                ? 'bg-blue-500 text-white'");
                sb.AppendLine("                : 'bg-gray-200 text-gray-700'");
                sb.AppendLine("            }`}");
                sb.AppendLine("          >");
                sb.AppendLine("            {category.name}");
                sb.AppendLine("          </button>");
                sb.AppendLine("        ))}");
                sb.AppendLine("      </div>");
            }

            // Tag selector
            if (component.Children.Any(c => c.Name == "TagSelector"))
            {
                sb.AppendLine("      <div className=\"flex flex-wrap gap-2\">");
                sb.AppendLine("        {tags.map(tag => (");
                sb.AppendLine("          <label");
                sb.AppendLine("            key={tag.id}");
                sb.AppendLine("            className=\"flex items-center space-x-2\"");
                sb.AppendLine("          >");
                sb.AppendLine("            <input");
                sb.AppendLine("              type=\"checkbox\"");
                sb.AppendLine("              checked={selectedTags.includes(tag.id)}");
                sb.AppendLine("              onChange={() => dispatch(toggleTag(tag.id))}");
                sb.AppendLine("            />");
                sb.AppendLine("            <span");
                sb.AppendLine("              className=\"inline-block w-3 h-3 rounded-full\"");
                sb.AppendLine("              style={{ backgroundColor: tag.color }}");
                sb.AppendLine("            />");
                sb.AppendLine("            <span>{tag.name}</span>");
                sb.AppendLine("          </label>");
                sb.AppendLine("        ))}");
                sb.AppendLine("      </div>");
            }

            // Todo items
            sb.AppendLine("      <ul className=\"space-y-2\">");
            sb.AppendLine("        {filteredTodos.map(todo => (");
            sb.AppendLine("          <li");
            sb.AppendLine("            key={todo.id}");
            sb.AppendLine("            className=\"flex items-center justify-between p-4 bg-white rounded shadow\"");
            sb.AppendLine("          >");
            sb.AppendLine("            <div className=\"flex items-center space-x-4\">");
            sb.AppendLine("              <input");
            sb.AppendLine("                type=\"checkbox\"");
            sb.AppendLine("                checked={todo.completed}");
            sb.AppendLine("                onChange={() => dispatch(toggleTodo(todo.id))}");
            sb.AppendLine("              />");
            sb.AppendLine("              <span className={todo.completed ? 'line-through' : ''}>");
            sb.AppendLine("                {todo.text}");
            sb.AppendLine("              </span>");
            sb.AppendLine("              <div className=\"flex gap-1\">");
            sb.AppendLine("                {todo.tagIds.map(tagId => {");
            sb.AppendLine("                  const tag = tags.find(t => t.id === tagId);");
            sb.AppendLine("                  return tag && (");
            sb.AppendLine("                    <span");
            sb.AppendLine("                      key={tag.id}");
            sb.AppendLine("                      className=\"px-2 py-1 text-xs rounded\"");
            sb.AppendLine("                      style={{ backgroundColor: tag.color }}");
            sb.AppendLine("                    >");
            sb.AppendLine("                      {tag.name}");
            sb.AppendLine("                    </span>");
            sb.AppendLine("                  );");
            sb.AppendLine("                })}");
            sb.AppendLine("              </div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("            <button");
            sb.AppendLine("              onClick={() => dispatch(removeTodo(todo.id))}");
            sb.AppendLine("              className=\"text-red-500 hover:text-red-700\"");
            sb.AppendLine("            >");
            sb.AppendLine("              ×");
            sb.AppendLine("            </button>");
            sb.AppendLine("          </li>");
            sb.AppendLine("        ))}");
            sb.AppendLine("      </ul>");
            sb.AppendLine("    </div>");
            sb.AppendLine("  );");
        }
    }
}