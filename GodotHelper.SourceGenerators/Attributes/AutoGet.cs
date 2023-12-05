using System;

namespace GodotHelper.SourceGenerators.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    [System.Diagnostics.Conditional("HelperGenerator_DEBUG")]
    public class AutoGetAttribute : Attribute
    {
        public string Path { get; }

        public bool NotNull { get; }

        /// <summary>
        /// 在 GetNodes() 中生成 name = GetNode&lt;T&gt;("path") .<br/>
        /// 不要忘记在 _Ready() 或者 _EnterTree() 中调用 GetNodes() !
        /// </summary>
        /// <param name="path">节点路径,默认获取变量名称为路径</param>
        /// <param name="notNull">如果允许空,则用 GetNodeOrNull() 获取节点</param>
        public AutoGetAttribute(string path = "", bool notNull = true)
        {
            Path = path;
            NotNull = notNull;
        }
    }
}
