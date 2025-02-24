using MarathonTranspiler.Core;
using MarathonTranspiler.Extensions;
using MarathonTranspiler.Transpilers.ReactRedux;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.FullStackWeb
{
    public class FullStackWebTranspiler : MarathonTranspilerBase
    {
        private TranspilerContext _currentContext = TranspilerContext.ReactRedux;
        private readonly ReactReduxTranspiler _reactTranspiler;
        private readonly AspNetTranspiler _aspNetTranspiler;
        private readonly ModelRelationshipHandler _modelHandler = new();
        private readonly ReduxRelationshipHandler _reduxHandler = new();
        private FullStackWebConfig _config;

        public FullStackWebTranspiler(FullStackWebConfig config)
        {
            this._config = config;
        }

        protected internal override void ProcessBlock(AnnotatedCode block, AnnotatedCode? previousBlock)
        {
            var mainAnnotation = block.Annotations[0];

            if (mainAnnotation.Name == "context")
            {
                _currentContext = mainAnnotation.Values.GetValue("type").ToLower() switch
                {
                    "reactredux" => TranspilerContext.ReactRedux,
                    "aspnetcoremvc" => TranspilerContext.AspNetCoreMvc,
                    _ => _currentContext
                };
                return;
            }

            // Handle class/model definitions in any context
            if (mainAnnotation.Name == "varInit" && mainAnnotation.Values.GetValue("type") == "class")
            {
                _modelHandler.ProcessModel(block);
            }

            switch (_currentContext)
            {
                case TranspilerContext.ReactRedux:
                    if (mainAnnotation.Name == "varInit")
                    {
                        _reduxHandler.ProcessReduxState(block);
                    }
                    else if (mainAnnotation.Name == "run" && mainAnnotation.Values.Any(v => v.Key == "isAsync"))
                    {
                        _reduxHandler.ProcessAsyncOperation(block);
                    }
                    _reactTranspiler.ProcessBlock(block, previousBlock);
                    break;

                case TranspilerContext.AspNetCoreMvc:
                    // Model handler has already processed the class definition if present
                    _aspNetTranspiler.ProcessBlock(block, previousBlock);
                    break;
            }
        }

        public override string GenerateOutput()
        {
            var sb = new StringBuilder();

            // Generate backend code first
            foreach (var model in _modelHandler.Models.Values)
            {
                var dbContextSb = new StringBuilder();
                _modelHandler.GenerateDbContext(dbContextSb);
                sb.AppendLine($"// {_config.Backend.DbContextName}.cs");
                sb.AppendLine(dbContextSb.ToString());
                sb.AppendLine();
            }
            sb.AppendLine(_aspNetTranspiler.GenerateOutput());

            // Then generate frontend code with relationship awareness
            foreach (var model in _modelHandler.Models.Values)
            {
                var storeName = $"{model.Name}Store";
                var storeSb = new StringBuilder();
                _reduxHandler.GenerateReduxSlice(storeSb, storeName);
                sb.AppendLine($"// {storeName.ToLower()}.ts");
                sb.AppendLine(storeSb.ToString());
                sb.AppendLine();
            }
            sb.AppendLine(_reactTranspiler.GenerateOutput());

            return sb.ToString();
        }
    }
}
