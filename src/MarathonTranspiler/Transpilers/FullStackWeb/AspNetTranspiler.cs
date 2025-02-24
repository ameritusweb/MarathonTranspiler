using MarathonTranspiler.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarathonTranspiler.Model.AspNet;
using MarathonTranspiler.Extensions;

namespace MarathonTranspiler.Transpilers.FullStackWeb
{
    public class AspNetTranspiler : MarathonTranspilerBase
    {
        private readonly AspNetConfig _config;
        private readonly Dictionary<string, ControllerInfo> _controllers = new();
        private readonly Dictionary<string, ModelInfo> _models = new();

        public AspNetTranspiler(AspNetConfig config)
        {
            this._config = config;
        }

        public bool HasContent => _controllers.Any() || _models.Any();

        protected internal override void ProcessBlock(AnnotatedCode block, AnnotatedCode? previousBlock)
        {
            var annotation = block.Annotations[0];

            switch (annotation.Name)
            {
                case "controller":
                    ProcessController(block);
                    break;

                case "varInit":
                    if (annotation.Values.GetValue("type") == "class")
                    {
                        ProcessModel(block);
                    }
                    break;

                case "run":
                    ProcessEndpoint(block);
                    break;
            }
        }

        private void ProcessController(AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var controllerName = annotation.Values.GetValue("className");

            if (!_controllers.ContainsKey(controllerName))
            {
                _controllers[controllerName] = new ControllerInfo { Name = controllerName };
            }
        }

        private void ProcessModel(AnnotatedCode block)
        {
            var className = block.Annotations[0].Values.GetValue("className");
            var model = new ModelInfo { Name = className };

            foreach (var line in block.Code)
            {
                var parts = line.Split(' ');
                model.Properties.Add(new PropertyInfo
                {
                    Type = parts[1],
                    Name = parts[2].TrimEnd(';')
                });
            }

            _models[className] = model;
        }

        private void ProcessEndpoint(AnnotatedCode block)
        {
            var annotation = block.Annotations[0];
            var controllerName = annotation.Values.GetValue("className");
            var method = new EndpointInfo
            {
                Name = annotation.Values.GetValue("functionName"),
                HttpMethod = annotation.Values.GetValue("httpMethod"),
                Route = annotation.Values.GetValue("route", ""),
                Code = block.Code
            };

            foreach (var param in block.Annotations.Skip(1))
            {
                if (param.Name == "parameter")
                {
                    method.Parameters.Add(new ParameterInfo
                    {
                        Name = param.Values.GetValue("name"),
                        Type = param.Values.GetValue("type")
                    });
                }
            }

            _controllers[controllerName].Endpoints.Add(method);
        }

        private string InferReturnType(List<string> code)
        {
            var returnLine = code.FirstOrDefault(line => line.TrimStart().StartsWith("return"));
            if (returnLine == null) return "void";

            if (returnLine.Contains("NotFound()")) return "ActionResult";
            if (returnLine.Contains("Ok(")) return "ActionResult";

            // Try to infer from context usage
            if (returnLine.Contains("_context"))
            {
                var modelName = _controllers.Keys.FirstOrDefault(k => returnLine.Contains($"_context.{k}s"));
                if (modelName != null) return $"List<{modelName}>";
            }

            return "object";
        }

        private string GenerateModels()
        {
            var sb = new StringBuilder();
            foreach (var model in _models.Values)
            {
                sb.AppendLine($"public class {model.Name}");
                sb.AppendLine("{");
                foreach (var prop in model.Properties)
                {
                    sb.AppendLine($"    public {prop.Type} {prop.Name} {{ get; set; }}");
                }
                sb.AppendLine("}");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private string GenerateMediatRHandlers()
        {
            var sb = new StringBuilder();
            foreach (var controller in _controllers.Values)
            {
                foreach (var endpoint in controller.Endpoints)
                {
                    var commandName = $"{endpoint.Name}Command";
                    sb.AppendLine($"public class {commandName} : IRequest<{InferReturnType(endpoint.Code)}>");
                    sb.AppendLine("{");
                    foreach (var param in endpoint.Parameters)
                    {
                        sb.AppendLine($"    public {param.Type} {param.Name} {{ get; set; }}");
                    }
                    sb.AppendLine("}");
                    sb.AppendLine();

                    sb.AppendLine($"public class {commandName}Handler : IRequestHandler<{commandName}, {InferReturnType(endpoint.Code)}>");
                    sb.AppendLine("{");
                    sb.AppendLine($"    private readonly {_config.DbContextName} _context;");
                    sb.AppendLine();
                    sb.AppendLine($"    public {commandName}Handler({_config.DbContextName} context)");
                    sb.AppendLine("    {");
                    sb.AppendLine("        _context = context;");
                    sb.AppendLine("    }");
                    sb.AppendLine();
                    sb.AppendLine($"    public async Task<{InferReturnType(endpoint.Code)}> Handle({commandName} request, CancellationToken cancellationToken)");
                    sb.AppendLine("    {");
                    foreach (var line in endpoint.Code)
                    {
                        sb.AppendLine($"        {line}");
                    }
                    sb.AppendLine("    }");
                    sb.AppendLine("}");
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }

        public override string GenerateOutput()
        {
            var sb = new StringBuilder();

            // Models
            sb.AppendLine("// Models");
            sb.AppendLine(GenerateModels());
            sb.AppendLine();

            // Controllers or Minimal API
            if (_config.UseMinimalApi)
            {
                var minimalApiSb = new StringBuilder();
                GenerateMinimalApi();
                sb.AppendLine("// Program.cs (Minimal API)");
                sb.AppendLine(minimalApiSb.ToString());
            }
            else
            {
                var controllersSb = new StringBuilder();
                GenerateControllers();
                sb.AppendLine("// Controllers");
                sb.AppendLine(controllersSb.ToString());
            }
            sb.AppendLine();

            // DbContext
            var dbContextSb = new StringBuilder();
            GenerateDbContext();
            sb.AppendLine($"// {_config.DbContextName}.cs");
            sb.AppendLine(dbContextSb.ToString());
            sb.AppendLine();

            // MediatR Handlers if enabled
            if (_config.UseMediatR)
            {
                var mediatrSb = new StringBuilder();
                mediatrSb.AppendLine(GenerateMediatRHandlers());
                sb.AppendLine("// MediatR Handlers");
                sb.AppendLine(mediatrSb.ToString());
            }

            return sb.ToString();
        }

        private void GenerateControllers()
        {
            foreach (var controller in _controllers.Values)
            {
                var sb = new StringBuilder();
                sb.AppendLine("using Microsoft.AspNetCore.Mvc;");
                sb.AppendLine();

                sb.AppendLine($"public class {controller.Name}Controller : ControllerBase");
                sb.AppendLine("{");

                // Constructor with DI
                sb.AppendLine($"    private readonly {_config.DbContextName} _context;");
                sb.AppendLine($"    public {controller.Name}Controller({_config.DbContextName} context)");
                sb.AppendLine("    {");
                sb.AppendLine("        _context = context;");
                sb.AppendLine("    }");

                // Endpoints
                foreach (var endpoint in controller.Endpoints)
                {
                    sb.AppendLine($"    [Http{endpoint.HttpMethod}(\"{endpoint.Route}\")]");
                    var returnType = InferReturnType(endpoint.Code);
                    var parameters = string.Join(", ", endpoint.Parameters
                        .Select(p => $"{p.Type} {p.Name}"));
                    sb.AppendLine($"    public async Task<ActionResult<{returnType}>> {endpoint.Name}({parameters})");
                    sb.AppendLine("    {");
                    foreach (var line in endpoint.Code)
                    {
                        sb.AppendLine($"        {line}");
                    }
                    sb.AppendLine("    }");
                }

                sb.AppendLine("}");

                File.WriteAllText($"{controller.Name}Controller.cs", sb.ToString());
            }
        }

        private void GenerateMinimalApi()
        {
            var sb = new StringBuilder();
            sb.AppendLine("var builder = WebApplication.CreateBuilder(args);");
            // Add services
            sb.AppendLine("var app = builder.Build();");

            foreach (var controller in _controllers.Values)
            {
                foreach (var endpoint in controller.Endpoints)
                {
                    sb.AppendLine($"app.Map{endpoint.HttpMethod}(\"/api/{controller.Name.ToLower()}/{endpoint.Route}\", async ({string.Join(", ", endpoint.Parameters.Select(p => $"{p.Type} {p.Name}"))}) =>");
                    sb.AppendLine("{");
                    foreach (var line in endpoint.Code)
                    {
                        sb.AppendLine($"    {line}");
                    }
                    sb.AppendLine("});");
                }
            }

            File.WriteAllText("Program.cs", sb.ToString());
        }

        private void GenerateDbContext()
        {
            var sb = new StringBuilder();
            sb.AppendLine("using Microsoft.EntityFrameworkCore;");
            sb.AppendLine();
            sb.AppendLine($"public class {_config.DbContextName} : DbContext");
            sb.AppendLine("{");

            foreach (var model in _models.Values)
            {
                sb.AppendLine($"    public DbSet<{model.Name}> {model.Name}s {{ get; set; }}");
            }

            sb.AppendLine("}");

            File.WriteAllText($"{_config.DbContextName}.cs", sb.ToString());
        }
    }
}
