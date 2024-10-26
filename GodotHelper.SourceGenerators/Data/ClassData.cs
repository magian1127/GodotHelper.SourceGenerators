using System;
using System.Collections.Generic;
using System.Text;

namespace GodotHelper.SourceGenerators.Data
{
    public readonly record struct ClassData
    {
        public readonly string Name { get; }
        public readonly string ClassNamespace { get; }
        public readonly string HintName { get; }
        public readonly string Path { get; }
        public readonly Dictionary<string, string> Methods { get; }

        public ClassData(string name, string classNamespace, string hintName, string path, Dictionary<string, string> methods)
        {
            Name = name;
            ClassNamespace = classNamespace;
            HintName = hintName;
            Path = path;
            Methods = methods;
        }
    }
}
