using System;
using System.Collections.Generic;
using System.Text;

namespace GodotHelper.SourceGenerators.Data
{
    public readonly record struct AutoLoadData
    {
        public readonly bool BaseIsAutoload { get; }
        public readonly string ClassNamespace { get; }
        public readonly string HintName { get; }
        public readonly string Name { get; }
        public readonly string FullName { get; }

        public AutoLoadData(bool baseIsAutoload, string classNamespace, string hintName, string name, string fullName)
        {
            BaseIsAutoload = baseIsAutoload;
            ClassNamespace = classNamespace;
            HintName = hintName;
            Name = name;
            FullName = fullName;
        }
    }
}
