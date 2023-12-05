using GodotHelper.SourceGenerators.Properties;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GodotHelper.SourceGenerators
{
    [Generator]
    public class NotifyGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForPostInitialization((i) => i.AddSource("HelperGenerator_NotifyAttribute.g", Resources.Notify));
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxContextReceiver is not SyntaxReceiver receiver) return;

            INamedTypeSymbol attributeSymbol = context.Compilation.GetTypeByMetadataName(ClassFullName.NotifyAttr);

            foreach (IGrouping<INamedTypeSymbol, IFieldSymbol> group in receiver.Fields.GroupBy<IFieldSymbol, INamedTypeSymbol>(f => f.ContainingType, SymbolEqualityComparer.Default))
            {
                ProcessClass(context, group.Key, [.. group], attributeSymbol);
            }
        }

        /// <summary>
        /// 处理脚本类
        /// </summary>
        /// <param name="context"></param>
        /// <param name="classSymbol">类符号</param>
        /// <param name="fields">需要处理的字段</param>
        /// <param name="attributeSymbol">特性符号</param>
        private void ProcessClass(GeneratorExecutionContext context, INamedTypeSymbol classSymbol, List<IFieldSymbol> fields, ISymbol attributeSymbol)
        {
            if (!classSymbol.ContainingSymbol.Equals(classSymbol.ContainingNamespace, SymbolEqualityComparer.Default))
            {
                return;
            }

            string namespaceName = classSymbol.ContainingNamespace.ToDisplayString();
            string uniqueHint = classSymbol.FullQualifiedNameOmitGlobal().SanitizeQualifiedNameForUniqueHint()
                               + "_GodotHelperNotify.g";

            StringBuilder source = new($@"using Godot;
using System;
using System.Collections.Generic;

namespace {namespaceName}
{{
    public partial class {classSymbol.Name}
    {{
");

            foreach (IFieldSymbol fieldSymbol in fields)
            {
                ProcessField(source, fieldSymbol, attributeSymbol);
            }

            source.Append("} }");
            context.AddSource(uniqueHint, SourceText.From(source.ToString(), Encoding.UTF8));
        }

        private void ProcessField(StringBuilder source, IFieldSymbol fieldSymbol, ISymbol attributeSymbol)
        {
            string fieldName = fieldSymbol.Name;
            ITypeSymbol fieldType = fieldSymbol.Type;

            AttributeData attributeData = fieldSymbol.GetAttributes().Single(ad => ad.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default));
            //bool useSignal = (bool)attributeData.ConstructorArguments[0].Value;
            //bool useExport = (bool)attributeData.ConstructorArguments[1].Value;
            // 两个参数无法使用,因为源生成器暂时还不能嵌套生成
            bool useSignal = false;
            bool useExport = false;

            string propertyName = fieldName.ToPascalCase();
            if (propertyName == fieldName)
            {// 属性名和变量名相同,直接不生成
                return;
            }

            if (useSignal)
            {
                source.Append($"    [Signal] public delegate void {propertyName}ChangedEventHandler({fieldType} oldValue, {fieldType} newValue);");
            }
            else
            {
                source.Append($"    public event Action<{fieldType}, {fieldType}> {propertyName}Changed;");
            }

            source.Append($@"
    partial void On{propertyName}Changing({fieldType} oldValue, {fieldType} newValue);
    partial void On{propertyName}Changed({fieldType} oldValue, {fieldType} newValue);
    
    /// <inheritdoc cref=""{fieldName}""/>
    {(useExport ? "[Export] " : "")}public {fieldType} {propertyName} 
    {{
        get => {fieldName};
        set
        {{
            if (!EqualityComparer<{fieldType}>.Default.Equals({fieldName}, value))
            {{
                var oldValue = {fieldName};
                On{propertyName}Changing(oldValue, value);
                {fieldName} = value;
                On{propertyName}Changed(oldValue, value);
                {(useSignal ? $"Emit{propertyName}Changed(oldValue, value);" : $"{propertyName}Changed?.Invoke(oldValue, value);")}
            }}
        }}
    }}
");
        }

        class SyntaxReceiver : ISyntaxContextReceiver
        {
            public List<IFieldSymbol> Fields { get; } = [];

            /// <summary>
            /// 在编译中对每个语法节点调用
            /// </summary>
            public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
            {
                if (context.Node is FieldDeclarationSyntax fieldDeclarationSyntax && fieldDeclarationSyntax.AttributeLists.Count > 0)
                {// 是否是字段声明语法, 并且包含特性.
                    foreach (VariableDeclaratorSyntax variable in fieldDeclarationSyntax.Declaration.Variables)
                    {
                        IFieldSymbol fieldSymbol = context.SemanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
                        if (fieldSymbol.GetAttributes().Any(ad => ad.AttributeClass.ToDisplayString() == ClassFullName.NotifyAttr))
                        {
                            Fields.Add(fieldSymbol);
                        }
                    }
                }
            }
        }
    }
}
