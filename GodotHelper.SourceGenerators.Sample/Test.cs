using Godot;
using GodotHelper.SourceGenerators.Attributes;
using System;

public partial class Test : Node2D
{
    [Notify]
    int _hp = 1;

    [AutoGet()]
    Button ButtonHpAdd;

    [AutoGet(nameof(ButtonHp0))]
    Button ButtonHp0;

    [AutoGet(notNull: false)]
    Node NodeA;

    [Signal]
    public delegate void DeathEventHandler(int hp);

    public void OnDeath(int hp)
    {
        GD.Print($"{nameof(OnDeath)} > hp {hp}");
    }

    public override void _Ready()
    {
        GetNodes();

        GD.Print($"AutoLoad {AutoLoad.AutoLoadGD.Name} can get");
        GD.Print($"AutoLoad {AutoLoad.AutoLoadCS.Name} can get");
        GD.Print($"AutoLoad {AutoLoad.InheritAutoLoadCS.Name} can get");

        GD.Print($"AutoLoad AutoLoadCS==InheritAutoLoadCS {AutoLoad.InheritAutoLoadCS == AutoLoad.AutoLoadCS}");

        GD.Print($"{ButtonHpAdd.Name} can get");
        GD.Print($"{ButtonHp0.Name} can get");
        RpcRegisterPlayer("play1");

        HpChanged += (oldValue, newValue) => GD.Print($"{nameof(HpChanged)}: {oldValue} -> {newValue}");

        Death += OnDeath;
    }

    partial void OnHpChanging(int oldValue,ref int newValue)
    {
        GD.Print($"{nameof(OnHpChanging)}: {oldValue} -> {newValue}");
    }

    partial void OnHpChanged(int oldValue, int newValue)
    {
        GD.Print($"{nameof(OnHpChanged)}: {oldValue} -> {newValue}");
        if (newValue < 1)
        {
            EmitDeath(newValue);
        }
    }

    private void OnButtonPressed()
    {
        Hp++;
    }

    private void OnButtonHp0Pressed()
    {
        Hp = 0;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void RegisterPlayer(string newPlayerName)
    {
        GD.Print($"{nameof(RegisterPlayer)}: {newPlayerName}");
    }

    public override void _Input(InputEvent @event)
    {
        using var _ = @event;
        if (@event.IsActionPressed(InputActionName.MoveUp))
        {
            Hp++;
        }

        if (@event.IsActionPressed(InputActionName.MoveDown))
        {
           Hp--;
        }
    }

    private void OnReplacingBy(Node node)
    {
        
    }
}
