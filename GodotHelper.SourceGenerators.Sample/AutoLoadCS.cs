using Godot;
using GodotHelper.SourceGenerators.Attributes;
using System;

[AutoLoadGet]
public partial class AutoLoadCS : Node2D
{
    partial void OnInit()
    {
        GD.Print($"{this.GetType().Name}.{nameof(AutoLoadCS)}.OnInit");
    }

    public override void _EnterTree()
    {
        GD.Print($"{Name}.{nameof(AutoLoadCS)}._EnterTree");
    }

    public override void _Ready()
    {
        GD.Print($"{Name}.{nameof(AutoLoadCS)}._Ready");
    }

    partial void OnReady()
    {
        GD.Print($"{Name}.{nameof(AutoLoadCS)}.OnReady");
    }
}
