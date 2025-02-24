using MarathonTranspiler.Core;
using MarathonTranspiler.Extensions;
using MarathonTranspiler.Model.AspNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.FullStackWeb
{
    public class ModelRelationshipHandler
    {
        private readonly Dictionary<string, ModelInfo> _models = new();
        private readonly Dictionary<string, List<Relationship>> _relationships = new();

        public class ModelInfo
        {
            public string Name { get; set; }
            public ModelType Type { get; set; }
            public List<Property> Properties { get; set; } = new();
        }

        public void ProcessModel(AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var modelName = annotation.Values.GetValue("className");
            var modelType = annotation.Values.GetValue("relationship", "entity").ToLower() switch
            {
                "aggregate" => ModelType.Aggregate,
                "join" => ModelType.Join,
                _ => ModelType.Entity
            };

            var model = new ModelInfo { Name = modelName, Type = modelType };

            foreach (var line in block.Code)
            {
                var property = ParseProperty(line);
                model.Properties.Add(property);

                if (property.IsNavigation)
                {
                    ProcessRelationship(modelName, property);
                }
            }

            _models[modelName] = model;
        }

        private Property ParseProperty(string line)
        {
            var parts = line.Split(' ');
            var type = parts[1];
            var name = parts[2].TrimEnd(';');

            return new Property
            {
                Name = name,
                Type = type,
                IsNavigation = type.Contains("ICollection") || _models.ContainsKey(type),
                IsCollection = type.Contains("ICollection")
            };
        }

        private void ProcessRelationship(string sourceModel, Property property)
        {
            var targetType = property.IsCollection
                ? property.Type.Replace("ICollection<", "").Replace(">", "")
                : property.Type;

            if (_models.ContainsKey(targetType))
            {
                var targetModel = _models[targetType];

                if (targetModel.Type == ModelType.Join)
                {
                    // Many-to-Many relationship
                    var otherEnd = targetModel.Properties
                        .First(p => p.IsNavigation && p.Type != sourceModel);

                    _relationships[sourceModel].Add(new Relationship
                    {
                        SourceModel = sourceModel,
                        TargetModel = otherEnd.Type,
                        Type = RelationType.ManyToMany,
                        JoinModel = targetType
                    });
                }
                else
                {
                    // One-to-Many or One-to-One
                    _relationships[sourceModel].Add(new Relationship
                    {
                        SourceModel = sourceModel,
                        TargetModel = targetType,
                        Type = property.IsCollection ? RelationType.OneToMany : RelationType.OneToOne
                    });
                }
            }
        }

        public void GenerateDbContext(StringBuilder sb)
        {
            sb.AppendLine("protected override void OnModelCreating(ModelBuilder modelBuilder)");
            sb.AppendLine("{");

            foreach (var relationship in _relationships.SelectMany(r => r.Value))
            {
                switch (relationship.Type)
                {
                    case RelationType.OneToMany:
                        sb.AppendLine($"    modelBuilder.Entity<{relationship.SourceModel}>()");
                        sb.AppendLine($"        .HasMany(e => e.{relationship.TargetModel}s)");
                        sb.AppendLine($"        .WithOne(e => e.{relationship.SourceModel})");
                        sb.AppendLine($"        .HasForeignKey(e => e.{relationship.SourceModel}Id);");
                        break;

                    case RelationType.ManyToMany:
                        sb.AppendLine($"    modelBuilder.Entity<{relationship.JoinModel}>()");
                        sb.AppendLine($"        .HasKey(e => new {{ e.{relationship.SourceModel}Id, e.{relationship.TargetModel}Id }});");
                        sb.AppendLine();
                        sb.AppendLine($"    modelBuilder.Entity<{relationship.JoinModel}>()");
                        sb.AppendLine($"        .HasOne(e => e.{relationship.SourceModel})");
                        sb.AppendLine($"        .WithMany(e => e.{relationship.JoinModel}s)");
                        sb.AppendLine($"        .HasForeignKey(e => e.{relationship.SourceModel}Id);");
                        sb.AppendLine();
                        sb.AppendLine($"    modelBuilder.Entity<{relationship.JoinModel}>()");
                        sb.AppendLine($"        .HasOne(e => e.{relationship.TargetModel})");
                        sb.AppendLine($"        .WithMany(e => e.{relationship.JoinModel}s)");
                        sb.AppendLine($"        .HasForeignKey(e => e.{relationship.TargetModel}Id);");
                        break;
                }
                sb.AppendLine();
            }

            sb.AppendLine("}");
        }

        public void GenerateApiEndpoints(string modelName, StringBuilder sb)
        {
            var model = _models[modelName];
            var relationships = _relationships[modelName];

            foreach (var relationship in relationships)
            {
                switch (relationship.Type)
                {
                    case RelationType.OneToMany:
                        sb.AppendLine($"    [HttpGet(\"{relationship.TargetModel}s\")]");
                        sb.AppendLine($"    public async Task<ActionResult<IEnumerable<{relationship.TargetModel}>>> " +
                            $"Get{relationship.TargetModel}s(int id)");
                        sb.AppendLine("    {");
                        sb.AppendLine($"        return await _context.{relationship.TargetModel}s");
                        sb.AppendLine($"            .Where(e => e.{relationship.SourceModel}Id == id)");
                        sb.AppendLine("            .ToListAsync();");
                        sb.AppendLine("    }");
                        break;

                    case RelationType.ManyToMany:
                        sb.AppendLine($"    [HttpGet(\"{relationship.TargetModel}s\")]");
                        sb.AppendLine($"    public async Task<ActionResult<IEnumerable<{relationship.TargetModel}>>> " +
                            $"Get{relationship.TargetModel}s(int id)");
                        sb.AppendLine("    {");
                        sb.AppendLine($"        return await _context.{relationship.JoinModel}s");
                        sb.AppendLine($"            .Where(e => e.{relationship.SourceModel}Id == id)");
                        sb.AppendLine($"            .Select(e => e.{relationship.TargetModel})");
                        sb.AppendLine("            .ToListAsync();");
                        sb.AppendLine("    }");

                        sb.AppendLine($"    [HttpPost(\"{relationship.TargetModel}s\")]");
                        sb.AppendLine($"    public async Task<ActionResult> Add{relationship.TargetModel}s(" +
                            $"int id, [FromBody] int[] {relationship.TargetModel}Ids)");
                        sb.AppendLine("    {");
                        sb.AppendLine($"        foreach (var targetId in {relationship.TargetModel}Ids)");
                        sb.AppendLine("        {");
                        sb.AppendLine($"            _context.{relationship.JoinModel}s.Add(new {relationship.JoinModel}");
                        sb.AppendLine("            {");
                        sb.AppendLine($"                {relationship.SourceModel}Id = id,");
                        sb.AppendLine($"                {relationship.TargetModel}Id = targetId");
                        sb.AppendLine("            });");
                        sb.AppendLine("        }");
                        sb.AppendLine("        await _context.SaveChangesAsync();");
                        sb.AppendLine("        return Ok();");
                        sb.AppendLine("    }");
                        break;
                }
                sb.AppendLine();
            }
        }

        public void GenerateTypeScript(StringBuilder sb)
        {
            foreach (var model in _models.Values)
            {
                sb.AppendLine($"export interface {model.Name} {{");
                foreach (var prop in model.Properties.Where(p => !p.IsNavigation))
                {
                    var tsType = GetTypeScriptType(prop.Type);
                    sb.AppendLine($"  {prop.Name}: {tsType};");
                }
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        private string GetTypeScriptType(string csharpType) => csharpType.ToLower() switch
        {
            "int" => "number",
            "long" => "number",
            "decimal" => "number",
            "bool" => "boolean",
            "datetime" => "Date",
            _ => "string"
        };
    }
}
