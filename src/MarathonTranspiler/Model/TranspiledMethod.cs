﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarathonTranspiler.Model
{
    public class TranspiledMethod
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<string> Parameters { get; set; } = new();
        public List<string> Code { get; set; } = new();
        public bool IsAbstract { get; set; }
        public string? ReturnType { get; set; }
        public string? Modifier { get; set; }
        public bool IsStatic { get; set; }
        public bool IsProperty { get; set; }
        public bool IsCoroutine { get; set; }
        public bool IsAutomatic { get; set; }
        public Dictionary<string, int> IndexById { get; set; } = new();
    }
}
