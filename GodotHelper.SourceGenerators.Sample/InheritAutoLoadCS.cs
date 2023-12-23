using Godot;
using GodotHelper.SourceGenerators.Attributes;
using System;

[AutoLoadGet]
public partial class InheritAutoLoadCS : AutoLoadCS
{
    partial void OnInit()
    {
        GD.Print($"{this.GetType().Name}.{nameof(InheritAutoLoadCS)}.OnInit");
    }

    public override void _EnterTree()
    {
        GD.Print($"{Name}.{nameof(InheritAutoLoadCS)}._EnterTree");
    }

    public override void _Ready()
    {
        GD.Print($"{Name}.{nameof(InheritAutoLoadCS)}._Ready");
    }

    partial void OnReady()
    {
        GD.Print($"{Name}.{nameof(InheritAutoLoadCS)}.OnReady");
    }
}
