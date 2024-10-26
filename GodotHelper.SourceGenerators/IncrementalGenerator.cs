using GodotHelper.SourceGenerators.Data;
using GodotHelper.SourceGenerators.Properties;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace GodotHelper.SourceGenerators
{
    [Generator]
    public class IncrementalGenerator : IIncrementalGenerator
    {
        private enum ProjectTag
        {
            AutoLoad, Input, Other
        }

        private enum TscnTag
        {
            ext_resource, node, connection, Other
        }

        private class Tscn
        {
            public string Path;

            public List<Tag> Tags = new();
        }

        private class Tag
        {
            public TscnTag Type;
            public string Name;
            public Dictionary<string, string> Propertys = new();
        }

        static string GodotDir;

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput((i) => i.AddSource("HelperGenerator_AutoLoadGetAttribute.g", Resources.AutoLoadGet));

            // 获取目标项目 csproj 配置中 AdditionalFiles 里指定的文件
            var godotFiles = context.AdditionalTextsProvider.Where(static file => Path.GetFileName(file.Path).Equals("project.godot"));

            IncrementalValuesProvider<List<(ProjectTag tag, string name, string path)>> godotContents = godotFiles.Select((additionalText, cancellationToken) =>
            {// 这里 Select 会处理上面(files)获取的每个文件, 但是目前只有一个 project.godot, 所以下面的方法都只是针对 project.godot 的.
                GodotDir = Path.GetDirectoryName(additionalText.Path);
                SourceText fileText = additionalText.GetText(cancellationToken);
                ProjectTag tag = ProjectTag.Other;
                int processNum = 0;//处理的标签数

                List<(ProjectTag tag, string name, string path)> list = new();
                foreach (TextLine line in fileText.Lines)
                {
                    string lineText = line.ToString();
                    if (string.IsNullOrWhiteSpace(lineText)) continue;

                    if (lineText.StartsWith("["))
                    {
                        switch (lineText)
                        {
                            case "[autoload]":
                                tag = ProjectTag.AutoLoad;
                                processNum++;
                                break;
                            case "[input]":
                                tag = ProjectTag.Input;
                                processNum++;
                                break;
                            default:
                                tag = ProjectTag.Other;
                                if (processNum > 1)
                                {//假设找到 autoload 的时候是 1, 再找到 input 的时候是 2 , 此时如果再找到新的标签, 那 2 就应该退出 foreach 了. 但是这里 break 退出的是 switch, 所以放到外面再退出.
                                    processNum++;
                                }
                                break;
                        }
                        if (processNum > 2)
                        {
                            break;
                        }
                        continue;//标签直接处理下一行
                    }

                    switch (tag)
                    {
                        case ProjectTag.AutoLoad:
                            var autoload = lineText.Split('=');
                            if (autoload.Length == 2)
                            {
                                list.Add((tag, autoload[0].Trim(), autoload[1].Trim('"')));
                            }
                            break;
                        case ProjectTag.Input:
                            var input = lineText.Split('=');
                            if (input.Length > 1)
                            {
                                list.Add((tag, input[0].Trim(), ""));
                            }
                            break;
                        default:
                            break;
                    }
                }
                return list;
            });

            // 获取所有标记过 AutoLoad 的类名称, Collect() 是将所有的值收集到一个集合中, 也就是把 IncrementalValuesProvider<T> 变为 IncrementalValueProvider<ImmutableArray<T>>, 方便后面 Combine() 使用;
            var autoloadClass = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    ClassFullName.AutoLoadGetAttr,
                    static (SyntaxNode sn, CancellationToken _) => true,
                    static (GeneratorAttributeSyntaxContext gasc, CancellationToken _) => GetAutoLoadData(gasc.SemanticModel, gasc.TargetNode))
                .Where(static d => d is not null).Collect();

            // 组合上面的处理结果,方便后面 RegisterSourceOutput() 使用, 因为它只能接收一个源(IncrementalValueProvider<T>类型).
            var godotValues = godotContents.Combine(autoloadClass);

            // 注册生成代码的方法, 第一个参数是传入上面增量值(godotValues), 第二个参数是一个自定义处理方法, 约等于 增量值 的 foreach.
            context.RegisterSourceOutput(godotValues, (sourceProductionContext, godotValue) =>
            {
                if (godotValue.Left == null)
                {
                    return;
                }

                Dictionary<string, AutoLoadData> autoloads = new();
                List<string> autoloadOthers = new();
                List<string> inputs = new();

                godotValue.Left.ForEach(x =>
                {
                    switch (x.tag)
                    {
                        case ProjectTag.AutoLoad:
                            var autoLoadData = godotValue.Right.SingleOrDefault(s => s.Value.Name == x.name);
                            if (autoLoadData != null)
                            {
                                autoloads.Add(x.name, autoLoadData.Value);
                            }
                            else
                            {
                                autoloadOthers.Add(x.name);
                            };
                            break;
                        case ProjectTag.Input:
                            inputs.Add(x.name);
                            break;
                        default:
                            break;
                    }
                });

                GeneratorAutoLoad(sourceProductionContext, autoloads, autoloadOthers);

                GeneratorInput(sourceProductionContext, inputs);

            });


            // 读取所有 tscn. 目前只处理 tscn 的信号引用 C# 方法的文件.
            var tscnFiles = context.AdditionalTextsProvider.Where(static file => Path.GetExtension(file.Path).Equals(".tscn"));

            var tscnContents = tscnFiles.Select(static (additionalText, cancellationToken) =>
            {
                SourceText fileText = additionalText.GetText(cancellationToken);

                Tscn tscn = new();
                tscn.Path = "res://" + RelativeToDir(additionalText.Path, GodotDir);
                Tag tag = new();
                foreach (TextLine line in fileText.Lines)
                {
                    string lineText = line.ToString();
                    if (string.IsNullOrWhiteSpace(lineText)) continue;

                    if (lineText.StartsWith("["))
                    {//标签是这样的 [node name="Node" groups=["a", "b"]]
                        var tagText = lineText.Substring(1, lineText.Length - 2).Split(' ');

                        if (Enum.TryParse(tagText[0], out TscnTag newTagType))
                        {
                            switch (newTagType)
                            {
                                case TscnTag.ext_resource:
                                    if (!lineText.Contains(".cs\""))
                                    {//暂时只记录有cs的
                                        continue;
                                    }
                                    break;
                                default:
                                    if (tag.Type == TscnTag.ext_resource)
                                    {// 上一个还是 ext_resource 标签, 当前的却不是, 说明已经检索全部了. 不处理没有 cs 脚本的 tscn.
                                        if (!tscn.Tags.Any(t => t.Type == TscnTag.ext_resource)) return null;
                                    }
                                    break;
                            }
                            tag = new Tag();
                            tag.Type = newTagType;
                            Dictionary<string, string> tagPropertys = new Dictionary<string, string>();
                            for (int i = 1; i < tagText.Length; i++)
                            {
                                var propertyTexts = tagText[i].Split('=');//"name=\"Node\"" 拆解为 {"name", "\"Node\""}
                                if (propertyTexts.Length > 1 && propertyTexts[1].Length > 2)
                                {// "\"Node\" 为什么不直接使用 Trim, 因为有些值是 "[\"a\", \"b\"]"
                                    tagPropertys.Add(propertyTexts[0], propertyTexts[1].Substring(1, propertyTexts[1].Length - 2));
                                }
                            }
                            tag.Propertys = tagPropertys;
                            switch (tag.Type)
                            {// 不同标签使用不同的属性设置名称
                                case TscnTag.ext_resource:
                                    if (tag.Propertys.ContainsKey("id"))
                                    {
                                        tag.Name = tag.Propertys["id"];
                                        tscn.Tags.Add(tag);
                                    }
                                    break;
                                case TscnTag.node:
                                    if (tag.Propertys.ContainsKey("name"))
                                    {
                                        tag.Name = tag.Propertys.ContainsKey("parent") ? tag.Propertys["name"] : ".";
                                        tscn.Tags.Add(tag);
                                    }
                                    break;
                                case TscnTag.connection:
                                    if (tag.Propertys.ContainsKey("method"))
                                    {
                                        tag.Name = tag.Propertys["method"];

                                        var nodeTag = tscn.Tags.SingleOrDefault(t => t.Type == TscnTag.node && t.Name == tag.Propertys["to"]);
                                        if (nodeTag != null)
                                        {
                                            var scriptTag = tscn.Tags.SingleOrDefault(t => t.Type == TscnTag.ext_resource && t.Name == nodeTag.Propertys["script"]);
                                            if (scriptTag != null)
                                            {
                                                tag.Propertys.Add("cs", scriptTag.Propertys["path"]);
                                                tscn.Tags.Add(tag);
                                            }
                                        }
                                    }
                                    break;
                                default:
                                    break;
                            }
                        }
                        else
                        {
                            tag = new();
                            tag.Type = TscnTag.Other;
                        }
                        continue;
                    }

                    switch (tag.Type)
                    {
                        case TscnTag.node:
                            var propertyTexts = lineText.Split('=');
                            var key = propertyTexts[0].Trim();
                            if (key == "script" && propertyTexts.Length > 1)
                            {
                                var value = propertyTexts[1].Trim();
                                value = value.Substring(13, value.Length - 15);// ExtResource("x_xxxxx") 获取 x_xxxxx
                                tag.Propertys.Add(key, value);
                            }
                            break;
                        default:
                            break;
                    }
                }

                // 没有连接信号到 C# 的暂时也不处理.
                if (!tscn.Tags.Any(t => t.Type == TscnTag.connection)) return null;

                return tscn;
            }).Where(static t => t is not null).Collect();

            var signalConnectionClass = context.SyntaxProvider
                    .CreateSyntaxProvider(
                        static (SyntaxNode sn, CancellationToken _) => sn is ClassDeclarationSyntax,
                        static (gsc, _) => GetClassData(gsc))
                    .Where(static d => d is not null);

            var tscnValues = signalConnectionClass.Combine(tscnContents);

            context.RegisterSourceOutput(tscnValues, (sourceProductionContext, tscnValues) =>
            {
                if (tscnValues.Left == null)
                {
                    return;
                }

                GeneratorConnectionTscn(sourceProductionContext, tscnValues.Left.Value, tscnValues.Right);
            });
        }

        private static void GeneratorAutoLoad(SourceProductionContext sourceProductionContext, Dictionary<string, AutoLoadData> autoloads, List<string> autoloadOthers)
        {
            if (autoloads.Count() > 0)
            {
                StringBuilder source = new();

                source.Append($@"using Godot;
using System;

    public partial class AutoLoad
    {{
");
                StringBuilder othersString = new();
                foreach (var item in autoloadOthers)
                {
                    source.Append($"        public static Node {item} {{ get; set; }} = null!;\n");
                    othersString.Append($"        AutoLoad.{item} ??= GetNode(\"/root/{item}\");\n");
                }

                bool getOthers = true;
                foreach (var item in autoloads)
                {
                    sourceProductionContext.AddSource(item.Value.HintName, $@"using Godot;
using System;
{(item.Value.ClassNamespace.Length > 0 ? $"\nnamespace {item.Value.ClassNamespace};\n" : "")}
public partial class {item.Value.Name}
{{
    partial void OnInit();
    public {item.Value.Name}()
    {{{(item.Value.BaseIsAutoload ? "\n        Ready -= base.ReadyCallback;" : "")}
        Ready += ReadyCallback;
        OnInit();
    }}

#pragma warning disable CS0109
    partial void OnReady();
    public new void ReadyCallback()
    {{
        AutoLoad.{item.Key} = this;
{(getOthers ? othersString : "")}
        OnReady();
    }}
#pragma warning restore CS0109
}}");
                    getOthers = false;
                    source.Append($"        public static {item.Value.FullName} {item.Key} {{ get; set; }} = null!;\n");
                }
                source.Append($@"
    }}
");
                sourceProductionContext.AddSource("HelperGenerator_AutoLoad.g.cs", source.ToString());
            }
        }

        private static void GeneratorInput(SourceProductionContext sourceProductionContext, List<string> inputs)
        {
            if (inputs.Count() > 0)
            {
                StringBuilder source = new();

                source.Append($@"using Godot;
using System;

    public partial class InputActionName
    {{
");

                foreach (var item in inputs)
                {
                    source.Append($"        public static readonly StringName {item} = \"{item}\";\n");
                }
                source.Append($@"
    }}
");
                sourceProductionContext.AddSource("HelperGenerator_InputActionName.g.cs", source.ToString());
            }
        }

        private static void GeneratorConnectionTscn(SourceProductionContext sourceProductionContext, ClassData classData, System.Collections.Immutable.ImmutableArray<Tscn> tscns)
        {
            var resPath = "res://" + RelativeToDir(classData.Path, GodotDir);
            var connectionTscns = tscns.Where(t => t.Tags.Any(tag => tag.Type == TscnTag.connection && tag.Propertys["cs"] == resPath));

            if (classData.Methods.Count() == 0 || connectionTscns.Count() == 0)
            {
                return;
            }

            StringBuilder sourceMethod = new();
            foreach (var item in classData.Methods)
            {
                foreach (var tscnItem in connectionTscns)
                {
                    var tags = tscnItem.Tags.Where(t => t.Type == TscnTag.connection && t.Name == item.Key && t.Propertys["cs"] == resPath);
                    foreach (var tagItem in tags)
                    {
                        sourceMethod.Append($@"
        // {tscnItem.Path} - FromNode: {tagItem.Propertys["from"]} - Signal: {tagItem.Propertys["signal"]}
        {item.Value}
");
                    }
                }
            }

            if (sourceMethod.Length == 0)
            {
                return;
            }

            StringBuilder source = new($@"using Godot;
using System;
using System.Collections.Generic;
{(classData.ClassNamespace.Length > 0 ? $"\nnamespace {classData.ClassNamespace};\n" : "")}
#pragma warning disable CS0162
#if TOOLS
public partial class {classData.Name}
{{
    public void GetMethodConnectionTscnList()
    {{
        return;
"
);
            source.Append(sourceMethod);
            source.Append($@"
    }}
}}
#endif // TOOLS
#pragma warning restore CS0162"
);
            string uniqueHint = classData.HintName + "_GodotHelper_ConnectionTscn.g";

            sourceProductionContext.AddSource(uniqueHint, source.ToString());
        }

        private static AutoLoadData? GetAutoLoadData(SemanticModel semanticModel, SyntaxNode declarationSyntax)
        {
            if (semanticModel.GetDeclaredSymbol(declarationSyntax) is not INamedTypeSymbol symbol) return null;

            bool baseIsAutoload = symbol.BaseType.GetAttributes().Any(a => a.AttributeClass.IsAutoLoadGetAttribute());
            string classNamespace = symbol.GetClassNamespace();
            string hintName = $"{symbol.FullQualifiedNameOmitGlobal().SanitizeQualifiedNameForUniqueHint()}_GodotHelper_AutoLoad.g.cs";
            string name = symbol.Name;
            string fullName = symbol.FullQualifiedNameIncludeGlobal();

            return new AutoLoadData(baseIsAutoload, classNamespace, hintName, name, fullName);
        }

        private static ClassData? GetClassData(GeneratorSyntaxContext gsc)
        {
            var classSymbol = (INamedTypeSymbol)gsc.SemanticModel.GetDeclaredSymbol(gsc.Node);

            if (classSymbol?.BaseType == null || !classSymbol.BaseType.InheritsFrom("GodotSharp", ClassFullName.GodotObject))
            {
                return null;
            }

            var members = classSymbol.GetMembers().OfType<IMethodSymbol>().Where(m => !m.IsStatic);
            Dictionary<string, string> methods = new();
            foreach (var item in members)
            {
                methods.Add(item.Name, item.GenerateMethodCall());
            }

            string classNamespace = classSymbol.GetClassNamespace();
            string hintName = classSymbol.FullQualifiedNameOmitGlobal().SanitizeQualifiedNameForUniqueHint();

            return new ClassData(classSymbol.Name, classNamespace, hintName, gsc.Node.SyntaxTree.FilePath, methods);
        }

        /// <summary>
        /// 根据目录生成相对路劲
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="dir">目录</param>
        /// <returns></returns>
        public static string RelativeToDir(string path, string dir)
        {
            dir = Path.Combine(dir, " ").TrimEnd();

            if (Path.DirectorySeparatorChar == '\\')
                dir = dir.Replace("/", "\\") + "\\";

            var fullPath = new Uri(Path.GetFullPath(path), UriKind.Absolute);
            var relRoot = new Uri(Path.GetFullPath(dir), UriKind.Absolute);

            return Uri.UnescapeDataString(relRoot.MakeRelativeUri(fullPath).ToString());
        }
    }
}
