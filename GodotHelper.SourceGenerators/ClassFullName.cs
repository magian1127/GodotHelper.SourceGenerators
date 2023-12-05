using System;
using System.Collections.Generic;
using System.Text;

namespace GodotHelper.SourceGenerators
{
    /// <summary>
    /// 所需要判定类的完整类型名称
    /// </summary>
    public static class ClassFullName
    {
        //Godot 内置
        public const string GodotObject = "Godot.GodotObject";
        public const string RpcAttr = "Godot.RpcAttribute";
        public const string SignalAttr = "Godot.SignalAttribute";

        // 自定义
        public const string AutoGetAttr = "GodotHelper.SourceGenerators.Attributes.AutoGetAttribute";
        public const string NotifyAttr = "GodotHelper.SourceGenerators.Attributes.NotifyAttribute";
    }
}
