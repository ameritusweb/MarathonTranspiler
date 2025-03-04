﻿using MarathonTranspiler.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Model
{
    public class TranspiledClass
    {
        public string ClassName { get; set; }
        public List<string> Fields { get; set; } = new();
        public List<TranspiledProperty> Properties { get; set; } = new();
        public List<TranspiledMethod> Methods { get; set; } = new();
        public List<string> ConstructorLines { get; set; } = new();
        public List<string> Assertions { get; set; } = new();
        public bool IsAbstract { get; set; }
        public string BaseClass { get; set; }
        public Dictionary<string, List<string>> AdditionalData { get; set; } = new();
        public List<TestStep> TestSteps { get; set; } = new();
        public List<InjectedDependency> Injections { get; set; } = new();
    }
}
