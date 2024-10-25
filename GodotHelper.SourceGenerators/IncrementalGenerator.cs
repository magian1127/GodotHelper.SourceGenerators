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

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput((i) => i.AddSource("HelperGenerator_AutoLoadGetAttribute.g", Resources.AutoLoadGet));

            // 获取目标项目 csproj 配置中 AdditionalFiles 里指定的文件
            var godotFiles = context.AdditionalTextsProvider.Where(static file => Path.GetFileName(file.Path).Equals("project.godot"));

            IncrementalValuesProvider<List<(ProjectTag tag, string name, string path)>> godotContents = godotFiles.Select((additionalText, cancellationToken) =>
            {// 这里 Select 会处理上面(files)获取的每个文件, 但是目前只有一个 project.godot, 所以下面的方法都只是针对 project.godot 的.
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

    }
}
