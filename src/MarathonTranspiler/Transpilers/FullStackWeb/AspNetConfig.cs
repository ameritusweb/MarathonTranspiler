using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Transpilers.FullStackWeb
{
    public class AspNetConfig
    {
        public string DbContextName { get; set; } = "ApplicationDbContext";
        public string OutputPath { get; set; } = "Api";
        public bool UseMinimalApi { get; set; }
        public bool GenerateSwagger { get; set; } = true;
        public bool UseMediatR { get; set; } = false;
        public string ApiVersion { get; set; } = "v1";
        public string AuthType { get; set; } = "JWT";
    }
}
