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
Some features also require / 部分功能还需要:
```xml
...
    <ItemGroup>
        <AdditionalFiles Include="project.godot" />
    </ItemGroup>
...
```

## Usage / 用法

- [AutoGet](#autoget)
- [RpcMethod](#rpcmethod)
- [EmitSignal](#emitsignal)
- [Notify](#notify)
- [AutoLoadGet](#autoloadget)
- [InputActionName](#inputactionname)

### `[AutoGet]`

First Code / 首先手写:

```cs
using GodotHelper.SourceGenerators.Attributes;
public partial class MyNode : Node2D
{
    [AutoGet] Button button;
    [AutoGet(nameof(NodeA))] Node NodeA;
    [AutoGet(notNull: false)] Node2D Node2D { get; set; }
    [AutoGet("Node/Label")] Label label;
}
```

Generated Code / 生成的代码:

```cs
    public void GetNodes()
    {
        button = GetNode<global::Godot.Button>("%button");
        NodeA = GetNode<global::Godot.Node>("NodeA");
        Node2D = GetNodeOrNull<global::Godot.Node2D>("%Node2D");
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

### `[AutoLoadGet]`

Require Set AdditionalFiles.
And Godot Set AutoLoad.

First Code / 首先手写:

```cs
using GodotHelper.SourceGenerators.Attributes;
[AutoLoadGet]
public partial class MyAutoLoad : Node2D
{
    ...
}
```

Generated Code / 生成的代码:

```cs
public partial class MyAutoLoad
{
    partial void OnInit();
    public MyAutoLoad()
    {
        Ready += ReadyCallback;
        OnInit();
    }

#pragma warning disable CS0109
    partial void OnReady();
    public new void ReadyCallback()
    {
        AutoLoad.MyAutoLoad = this;
        AutoLoad.MyAutoLoad2 ??= GetNode("/root/MyAutoLoad2");// is not c#

        OnReady();
    }
#pragma warning restore CS0109
}
```

```cs
    public partial class AutoLoad
    {
        public static Node MyAutoLoad2 { get; set; } = null!;
        public static global::MyAutoLoad MyAutoLoad { get; set; } = null!;
    }
}
```

Use / 使用:
```cs
    public override void _Ready()
    {
        AutoLoad.MyAutoLoad.XXX = XX;
        ...
    }
```

```cs
using GodotHelper.SourceGenerators.Attributes;
[AutoLoadGet]
public partial class MyAutoLoad : Node2D
{
    ...
    partial void OnInit()
    {
        // you code
    }

    partial void OnReady()
    {
        // you code
    }
    ...
}
```

### `[InputActionName]`

Require Set AdditionalFiles.
And Godot Set Input.

First Code / 首先手写:

```cs
need not
```

Generated Code / 生成的代码:

```cs
    public partial class InputActionName
    {
        public static readonly StringName MoveUp = "MoveUp";
        public static readonly StringName MoveDown = "MoveDown";

    }
```

Use / 使用:
```cs
    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed(InputActionName.MoveUp))
        {
            ...
        }
    }
```
