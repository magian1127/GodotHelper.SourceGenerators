# GodotHelper.SourceGenerators

Auxiliary source generator for the Godot project, with some methods extracted from `Godot.SourceGenerators`.

辅助 Godot 项目的源生成器, 部分方法提取自 `Godot.SourceGenerators`.

## Reference Project / 引用项目
```xml
...
  <ItemGroup>
    <ProjectReference Include="GodotHelper.SourceGenerators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
  </ItemGroup>
...
```

## Usage / 用法

### `[AutoGet]`

First Code / 首先手写:

```cs
using GodotHelper.SourceGenerators.Attributes;
public partial class MyNode : Node2D
{
    [AutoGet] Button button;
    [AutoGet(notNull: false)] Node2D Node2D { get; set; }
    [AutoGet("Node/Label")] Label label;
}
```

Generated Code / 生成的代码:

```cs
    public void GetNodes()
    {
        button = GetNode<global::Godot.Button>("%button");

        Node2D = GetNodeOrNull<global::Godot.Node2D>("Node2D");
        label = GetNode<global::Godot.Label>("Node/Label");
    }
```

Use / 使用:
```cs
    public override void _Ready()
    {
        GetNodes();
        ...
    }
```

### `RpcMethod()`

First Code / 首先手写:

```cs
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void RegisterPlayer(string newPlayerName)
    {
        ...
    }
```

Generated Code / 生成的代码:

```cs
    /// <inheritdoc cref="RegisterPlayer"/>
    public void RpcRegisterPlayer(string newPlayerName)
    {
        Rpc(MethodName.RegisterPlayer, newPlayerName);
    }
```

Use / 使用:
```cs
    public void XX()
    {
        RpcRegisterPlayer("play1");
    }
```

### `EmitSignal()`

First Code / 首先手写:

```cs
    [Signal] public delegate void DeathEventHandler();
```

Generated Code / 生成的代码:

```cs
    /// <inheritdoc cref="DeathEventHandler"/>
    public void EmitDeath(int hp)
    {
        EmitSignal(SignalName.Death, hp);
    }
```

Use / 使用:
```cs
    public void XX()
    {
        EmitDeath(-67);
    }
```

### `[Notify]`

First Code / 首先手写:

```cs
using GodotHelper.SourceGenerators.Attributes;
public partial class MyNode : Node2D
{
    [Notify] int _hp;
}
```

Generated Code / 生成的代码:

```cs
    public event Action<int, int> HpChanged;
    partial void OnHpChanging(int oldValue, int newValue);
    partial void OnHpChanged(int oldValue, int newValue);
    
    /// <inheritdoc cref="_hp"/>
    public int Hp 
    {
        get => _hp;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(_hp, value))
            {
                var oldValue = _hp;
                OnHpChanging(oldValue, value);
                _hp = value;
                OnHpChanged(oldValue, value);
                HpChanged?.Invoke(oldValue, value);
            }
        }
    }
```

Use / 使用:
```cs
public partial class MyNode : Node2D
{
    public void XX()
    {
        HpChanged += YY;
        Hp -= 1;
    }

    partial void OnHpChanging(int oldValue, int newValue)
    {
        // you code
    }

    partial void OnHpChanged(int oldValue, int newValue)
    {
        // you code
    }
}
```
