﻿//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

namespace GodotHelper.SourceGenerators.Properties {
    using System;
    
    
    /// <summary>
    ///   一个强类型的资源类，用于查找本地化的字符串等。
    /// </summary>
    // 此类是由 StronglyTypedResourceBuilder
    // 类通过类似于 ResGen 或 Visual Studio 的工具自动生成的。
    // 若要添加或移除成员，请编辑 .ResX 文件，然后重新运行 ResGen
    // (以 /str 作为命令选项)，或重新生成 VS 项目。
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   返回此类使用的缓存的 ResourceManager 实例。
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("GodotHelper.SourceGenerators.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   重写当前线程的 CurrentUICulture 属性，对
        ///   使用此强类型资源类的所有资源查找执行重写。
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   查找类似 using System;
        ///
        ///namespace GodotHelper.SourceGenerators.Attributes
        ///{
        ///    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
        ///    [System.Diagnostics.Conditional(&quot;HelperGenerator_DEBUG&quot;)]
        ///    public class AutoGetAttribute : Attribute
        ///    {
        ///        public string Path { get; }
        ///
        ///        public bool NotNull { get; }
        ///
        ///        /// &lt;summary&gt;
        ///        /// 在 GetNodes() 中生成 name = GetNode&amp;lt;T&amp;gt;(&quot;path&quot;) .&lt;br/&gt;
        ///        /// 不要忘记在 _Ready() 或者 _EnterT [字符串的其余部分被截断]&quot;; 的本地化字符串。
        /// </summary>
        internal static string AutoGet {
            get {
                return ResourceManager.GetString("AutoGet", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 using System;
        ///
        ///namespace GodotHelper.SourceGenerators.Attributes
        ///{
        ///    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
        ///    [System.Diagnostics.Conditional(&quot;HelperGenerator_DEBUG&quot;)]
        ///    public class AutoLoadGetAttribute : Attribute
        ///    {
        ///
        ///    }
        ///}
        /// 的本地化字符串。
        /// </summary>
        internal static string AutoLoadGet {
            get {
                return ResourceManager.GetString("AutoLoadGet", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 using System;
        ///
        ///namespace GodotHelper.SourceGenerators.Attributes
        ///{
        ///    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
        ///    [System.Diagnostics.Conditional(&quot;HelperGenerator_DEBUG&quot;)]
        ///    public class NotifyAttribute : Attribute
        ///    {
        ///        public bool UseSignal { get; }
        ///
        ///        public bool UseExport { get; }
        ///
        ///        /// &lt;summary&gt;
        ///        /// 生成对应的属性,并使用信号或事件发出通知.&lt;br/&gt;
        ///        /// 另外生成 partial 的 OnXXChanging 和 OnXXChanged 方法.&lt;br/&gt;
        ///        /// 两个参数无法使用,因为源生成 [字符串的其余部分被截断]&quot;; 的本地化字符串。
        /// </summary>
        internal static string Notify {
            get {
                return ResourceManager.GetString("Notify", resourceCulture);
            }
        }
    }
}
