using System;
using System.Collections.Generic;
using System.Text;

namespace GodotHelper.SourceGenerators.Data
{
    public readonly record struct AutoLoadData
    {
        public readonly bool BaseIsAutoload;
        public readonly string ClassNamespace;
        public readonly string HintName;
        public readonly string Name;
        public readonly string FullName;

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
