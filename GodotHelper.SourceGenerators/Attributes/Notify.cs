using System;

namespace GodotHelper.SourceGenerators.Attributes
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    [System.Diagnostics.Conditional("HelperGenerator_DEBUG")]
    public class NotifyAttribute : Attribute
    {
        public bool UseSignal { get; }

        public bool UseExport { get; }

        /// <summary>
        /// 生成对应的属性,并使用信号或事件发出通知.<br/>
        /// 另外生成 partial 的 OnXXChanging 和 OnXXChanged 方法.<br/>
        /// 两个参数无法使用,因为源生成器暂时还不能嵌套生成
        /// </summary>
        /// <param name="useSignal">使用 Godot 信号发出通知, 否则使用 Action</param>
        /// <param name="useExport">使用 Export 特新导出属性</param>
        public NotifyAttribute(bool useSignal = false, bool useExport = false)
        {
            UseSignal = useSignal;
            UseExport = useExport;
        }
    }
}
