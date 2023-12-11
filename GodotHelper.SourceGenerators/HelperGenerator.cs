using GodotHelper.SourceGenerators.Properties;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Text;

namespace GodotHelper.SourceGenerators
{
    [Generator]
    public class HelperGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            // 在目标项目中添加特性代码. 如果直接使用CS文件, 那么就需要目标项目在编译时引用本项目. (也就是去掉 ReferenceOutputAssembly="false" )
            context.RegisterForPostInitialization((i) => i.AddSource("HelperGenerator_AutoGetAttribute.g", Resources.AutoGet));
        }

        public void Execute(GeneratorExecutionContext context)
        {
            INamedTypeSymbol[] godotClasses = context
                .Compilation.SyntaxTrees
                .SelectMany(tree =>
                    tree.GetRoot().DescendantNodes()
                        .OfType<ClassDeclarationSyntax>()
                        .SelectGodotScriptClasses(context.Compilation)
                        .Select(x => x.symbol)
                )
                .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
                .ToArray();

            if (godotClasses.Length > 0)
            {
                foreach (var godotClass in godotClasses)
                {
                    ProcessClass(context, godotClass);
                }
            }
        }

        /// <summary>
        /// 处理 Godot 脚本类
        /// </summary>
        /// <param name="context"></param>
        /// <param name="symbol">类符号</param>
        private static void ProcessClass(GeneratorExecutionContext context, INamedTypeSymbol symbol)
        {
            bool needGenerate = false;
            var members = symbol.GetMembers();

            var autoGetProperties = members
                .Where(s => !s.IsStatic && s.Kind == SymbolKind.Property)
                .Cast<IPropertySymbol>()
                .Where(s => s.GetAttributes()
                    .Any(a => a.AttributeClass?.IsAutoGetAttribute() ?? false));
            needGenerate = needGenerate || autoGetProperties.Any();

            var autoGetFields = members
                .Where(s => !s.IsStatic && s.Kind == SymbolKind.Field && !s.IsImplicitlyDeclared)
                .Cast<IFieldSymbol>()
                .Where(s => s.GetAttributes()
                    .Any(a => a.AttributeClass?.IsAutoGetAttribute() ?? false));
            needGenerate = needGenerate || autoGetFields.Any();

            var rpcMethods = members
                .Where(s => s.Kind == SymbolKind.Method && !s.IsImplicitlyDeclared)
                .Cast<IMethodSymbol>()
                .Where(m => m.MethodKind == MethodKind.Ordinary)
                .Where(m => m.GetAttributes().Any(a => a.AttributeClass?.IsGodotRpcAttribute() ?? false));
            needGenerate = needGenerate || rpcMethods.Any();

            var signalDelegates = members
                 .Where(s => s.Kind == SymbolKind.NamedType)
                 .Cast<INamedTypeSymbol>()
                 .Where(namedTypeSymbol => namedTypeSymbol.TypeKind == TypeKind.Delegate)
                 .Where(s => s.GetAttributes()
                     .Any(a => a.AttributeClass?.IsGodotSignalAttribute() ?? false));
            needGenerate = needGenerate || signalDelegates.Any();

            if (!needGenerate) return;

            INamespaceSymbol namespaceSymbol = symbol.ContainingNamespace;
            string classNs = namespaceSymbol != null && !namespaceSymbol.IsGlobalNamespace ?
                namespaceSymbol.FullQualifiedNameOmitGlobal() :
                string.Empty;
            bool hasNamespace = classNs.Length != 0;

            bool isInnerClass = symbol.ContainingType != null;//是否是类中的类(内部类)

            string uniqueHint = symbol.FullQualifiedNameOmitGlobal().SanitizeQualifiedNameForUniqueHint()
                                + "_GodotHelper.g";

            var source = new StringBuilder();

            source.Append("using Godot;\n");
            source.Append("using Godot.NativeInterop;\n");
            source.Append("\n");

            if (hasNamespace)
            {
                source.Append("namespace ");
                source.Append(classNs);
                source.Append(" {\n\n");
            }

            if (isInnerClass)
            {
                var containingType = symbol.ContainingType;
                AppendPartialContainingTypeDeclarations(containingType);

                void AppendPartialContainingTypeDeclarations(INamedTypeSymbol containingType)
                {
                    if (containingType == null)
                        return;

                    AppendPartialContainingTypeDeclarations(containingType.ContainingType);

                    source.Append("partial ");
                    source.Append(containingType.GetDeclarationKeyword());
                    source.Append(" ");
                    source.Append(containingType.NameWithTypeParameters());
                    source.Append("\n{\n");
                }
            }

            source.Append("partial class ");
            source.Append(symbol.NameWithTypeParameters());
            source.Append("\n{\n");

            if (autoGetFields.Any() || autoGetProperties.Any())
            {//生成 AutoGet 特性相关代码
                source.Append($"    public void GetNodes()\n");
                source.Append("    {\n");

                foreach (var item in autoGetFields)
                {
                    var autoGet = item.GetAttributes().First(a => a.AttributeClass.IsAutoGetAttribute());
                    string path = autoGet.ConstructorArguments[0].Value?.ToString();
                    string nodePath = string.IsNullOrWhiteSpace(path) ? $"%{item.Name}" : path;
                    bool notNull = (bool)autoGet.ConstructorArguments[1].Value;
                    source.Append($"        {item.Name} = {(notNull ? "GetNode" : "GetNodeOrNull")}<{item.Type.FullQualifiedNameIncludeGlobal()}>(\"{nodePath}\");\n");
                }

                source.Append("\n");

                foreach (var item in autoGetProperties)
                {
                    var autoGet = item.GetAttributes().First(a => a.AttributeClass.IsAutoGetAttribute());
                    string path = autoGet.ConstructorArguments[0].Value?.ToString();
                    string nodePath = string.IsNullOrWhiteSpace(path) ? $"%{item.Name}" : path;
                    bool notNull = (bool)autoGet.ConstructorArguments[1].Value;
                    source.Append($"        {item.Name} = {(notNull ? "GetNode" : "GetNodeOrNull")}<{item.Type.FullQualifiedNameIncludeGlobal()}>(\"{nodePath}\");\n");
                }

                source.Append("    }\n\n");
            }

            foreach (var item in rpcMethods)
            {//生成 RPC 特性相关代码
                GenerateRpcMethod(source, item);
            }

            foreach (var item in signalDelegates)
            {//生成 Signal 特性相关代码
                GenerateEmitMethod(source, item);
            }

            source.Append("}\n"); // partial class

            if (isInnerClass)
            {
                var containingType = symbol.ContainingType;

                while (containingType != null)
                {
                    source.Append("}\n"); // outer class

                    containingType = containingType.ContainingType;
                }
            }

            if (hasNamespace)
            {
                source.Append("\n}\n");
            }

            context.AddSource(uniqueHint, SourceText.From(source.ToString(), Encoding.UTF8));
        }

        /// <summary>
        /// 生成使用 Rpc 的便捷方法
        /// </summary>
        /// <param name="source">需要添加到的源代码字符串</param>
        /// <param name="symbol">方法符号</param>
        private static void GenerateRpcMethod(StringBuilder source, IMethodSymbol symbol)
        {
            string paramsString = string.Empty;
            string argsString = string.Empty;
            for (int i = 0; i < symbol.Parameters.Length; i++)
            {
                if (i != 0)
                {
                    paramsString += ", ";
                }
                paramsString += $"{symbol.Parameters[i].Type.FullQualifiedNameIncludeGlobal()} {symbol.Parameters[i].Name}";
                argsString += $", {symbol.Parameters[i].Name}";
            }

            source.Append($"    /// <inheritdoc cref=\"{symbol.Name}\"/>\n");
            source.Append($"    public void Rpc{symbol.Name}(");
            source.Append(paramsString);
            source.Append(")\n");
            source.Append("    {\n");
            source.Append("        Rpc(MethodName.");
            source.Append(symbol.Name);
            source.Append(argsString);
            source.Append(");\n");
            source.Append("    }\n\n");

            source.Append($"    /// <inheritdoc cref=\"{symbol.Name}\"/>\n");
            source.Append($"    public void Rpc{symbol.Name}(long peerId");
            if (symbol.Parameters.Length > 0)
            {
                source.Append($", ");
            }
            source.Append(paramsString);
            source.Append(")\n");
            source.Append("    {\n");
            source.Append("        RpcId(peerId, MethodName.");
            source.Append(symbol.Name);
            source.Append(argsString);
            source.Append(");\n");
            source.Append("    }\n\n");
        }

        /// <summary>
        /// 生成发送信号的便捷方法
        /// </summary>
        /// <param name="source">需要添加到的源代码字符串</param>
        /// <param name="symbol">类型符号</param>
        private static void GenerateEmitMethod(StringBuilder source, INamedTypeSymbol symbol)
        {
            string paramsString = string.Empty;
            string argsString = string.Empty;

            if (symbol.DelegateInvokeMethod is IMethodSymbol methodSymbol)
            {
                for (int i = 0; i < methodSymbol.Parameters.Length; i++)
                {
                    if (i != 0)
                    {
                        paramsString += ", ";
                    }
                    paramsString += $"{methodSymbol.Parameters[i].Type.FullQualifiedNameIncludeGlobal()} {methodSymbol.Parameters[i].Name}";
                    argsString += $", {methodSymbol.Parameters[i].Name}";
                }
            }
            string signalName = symbol.Name;
            signalName = signalName.Substring(0, signalName.Length - "EventHandler".Length);

            source.Append($"    /// <inheritdoc cref=\"{symbol.FullQualifiedNameIncludeGlobal()}\"/>\n");
            source.Append($"    public void Emit{signalName}(");
            source.Append(paramsString);
            source.Append(")\n");
            source.Append("    {\n");
            source.Append("        EmitSignal(SignalName.");
            source.Append(signalName);
            source.Append(argsString);
            source.Append(");\n");
            source.Append("    }\n\n");

        }
    }
}
