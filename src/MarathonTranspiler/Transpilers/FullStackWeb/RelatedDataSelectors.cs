using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.FullStackWeb
{
    public class RelatedDataSelectors
    {
        public void GenerateMappedSelectors(StringBuilder sb)
        {
            // Normalized selectors
            sb.AppendLine("// Memoized selectors for related data");
            sb.AppendLine("export const selectTodosWithRelations = createSelector(");
            sb.AppendLine("  [selectTodos, selectCategories, selectTags],");
            sb.AppendLine("  (todos, categories, tags) => {");
            sb.AppendLine("    return todos.map(todo => ({");
            sb.AppendLine("      ...todo,");
            sb.AppendLine("      category: categories.find(c => c.id === todo.categoryId),");
            sb.AppendLine("      tags: todo.tagIds.map(id => tags.find(t => t.id === id)).filter(Boolean)");
            sb.AppendLine("    }));");
            sb.AppendLine("  }");
            sb.AppendLine(");");
            sb.AppendLine();

            // Filtered selectors
            sb.AppendLine("export const selectFilteredTodos = createSelector(");
            sb.AppendLine("  [selectTodosWithRelations, selectSelectedCategory, selectSelectedTags],");
            sb.AppendLine("  (todos, categoryId, selectedTags) => {");
            sb.AppendLine("    return todos.filter(todo => {");
            sb.AppendLine("      const matchesCategory = !categoryId || todo.categoryId === categoryId;");
            sb.AppendLine("      const matchesTags = !selectedTags.length ||");
            sb.AppendLine("        selectedTags.every(tagId => todo.tagIds.includes(tagId));");
            sb.AppendLine("      return matchesCategory && matchesTags;");
            sb.AppendLine("    });");
            sb.AppendLine("  }");
            sb.AppendLine(");");
        }

        public void GenerateDataFetching(StringBuilder sb)
        {
            sb.AppendLine("// Fetch all related data in parallel");
            sb.AppendLine("export const fetchAllData = createAsyncThunk(");
            sb.AppendLine("  'todoStore/fetchAllData',");
            sb.AppendLine("  async () => {");
            sb.AppendLine("    const [todos, categories, tags] = await Promise.all([");
            sb.AppendLine("      TodoApi.getAll(),");
            sb.AppendLine("      CategoryApi.getAll(),");
            sb.AppendLine("      TagApi.getAll()");
            sb.AppendLine("    ]);");
            sb.AppendLine("    return { todos, categories, tags };");
            sb.AppendLine("  }");
            sb.AppendLine(");");
        }
    }
}
