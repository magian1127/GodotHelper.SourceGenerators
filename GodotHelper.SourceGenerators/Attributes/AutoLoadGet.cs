using System;

namespace GodotHelper.SourceGenerators.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    [System.Diagnostics.Conditional("HelperGenerator_DEBUG")]
    public class AutoLoadGetAttribute : Attribute
    {

    }
}
