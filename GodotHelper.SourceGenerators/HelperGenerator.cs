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
                    VisitGodotScriptClass(context, godotClass);
                }
            }
        }

        private static void VisitGodotScriptClass(GeneratorExecutionContext context, INamedTypeSymbol symbol)
        {
            var members = symbol.GetMembers();

            var rpcMethods = members
                .Where(s => s.Kind == SymbolKind.Method && !s.IsImplicitlyDeclared)
                .Cast<IMethodSymbol>()
                .Where(m => m.MethodKind == MethodKind.Ordinary)
                .Where(m => m.GetAttributes().Any(a => a.AttributeClass?.IsGodotRpcAttribute() ?? false));


            var signalDelegate = members
                 .Where(s => s.Kind == SymbolKind.NamedType)
                 .Cast<INamedTypeSymbol>()
                 .Where(namedTypeSymbol => namedTypeSymbol.TypeKind == TypeKind.Delegate)
                 .Where(s => s.GetAttributes()
                     .Any(a => a.AttributeClass?.IsGodotSignalAttribute() ?? false));

            if (rpcMethods.Count() == 0 && signalDelegate.Count() == 0)
            {
                return;
            }

            INamespaceSymbol namespaceSymbol = symbol.ContainingNamespace;
            string classNs = namespaceSymbol != null && !namespaceSymbol.IsGlobalNamespace ?
                namespaceSymbol.FullQualifiedNameOmitGlobal() :
                string.Empty;
            bool hasNamespace = classNs.Length != 0;

            bool isInnerClass = symbol.ContainingType != null;

            string uniqueHint = symbol.FullQualifiedNameOmitGlobal().SanitizeQualifiedNameForUniqueHint()
                                + "_GodotHelper.generated";

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

            foreach (var item in rpcMethods)
            {
                GenerateRpcMethod(source, item);
            }

            foreach (var item in signalDelegate)
            {
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
