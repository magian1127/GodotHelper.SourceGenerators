using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GodotHelper.SourceGenerators
{
    /// <summary>
    /// 需要用到的扩展方法合集, 大部分提取自 Godot.SourceGenerators
    /// </summary>
    static class ExtensionMethods
    {
        /// <summary>
        /// 判断是否继承自指定的类型
        /// </summary>
        /// <param name="symbol">符号</param>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="typeFullName">类型的完全限定名</param>
        /// <returns></returns>
        public static bool InheritsFrom(this INamedTypeSymbol symbol, string assemblyName, string typeFullName)
        {
            while (symbol != null)
            {
                if (symbol.ContainingAssembly?.Name == assemblyName &&
                    symbol.FullQualifiedNameOmitGlobal() == typeFullName)
                {
                    return true;
                }

                symbol = symbol.BaseType;
            }

            return false;
        }

        /// <summary>
        /// 从 类声明语法 集合获取所有 Godot 脚本类符号集合
        /// </summary>
        /// <param name="source">类声明语法 集合</param>
        /// <param name="compilation">编译对象</param>
        /// <returns>返回 (类声明语法, 符号) 集合</returns>
        public static IEnumerable<(ClassDeclarationSyntax cds, INamedTypeSymbol symbol)> SelectGodotScriptClasses(
            this IEnumerable<ClassDeclarationSyntax> source,
            Compilation compilation
        )
        {
            foreach (var cds in source)
            {
                if (cds.TryGetGodotScriptClass(compilation, out var symbol))
                    yield return (cds, symbol!);
            }
        }

        /// <summary>
        /// 当前类声明语法中的类是否是 Godot 脚本类
        /// </summary>
        /// <param name="cds">类声明语法</param>
        /// <param name="compilation">编译对象</param>
        /// <param name="symbol">符号</param>
        /// <returns>返回 true, 则说明脚本是继承自 Godot 的类, 并赋值 symbol</returns>
        private static bool TryGetGodotScriptClass(
            this ClassDeclarationSyntax cds, Compilation compilation,
            out INamedTypeSymbol symbol
        )
        {
            var sm = compilation.GetSemanticModel(cds.SyntaxTree);

            var classTypeSymbol = sm.GetDeclaredSymbol(cds);

            if (classTypeSymbol?.BaseType == null
                || !classTypeSymbol.BaseType.InheritsFrom("GodotSharp", ClassFullName.GodotObject))
            {
                symbol = null;
                return false;
            }

            symbol = classTypeSymbol;
            return true;
        }

        /// <summary>
        /// 获取申明的关键字 (“class”、“struct”、“interface”、“record”) 
        /// </summary>
        /// <param name="namedTypeSymbol">类型符号</param>
        /// <returns></returns>
        public static string GetDeclarationKeyword(this INamedTypeSymbol namedTypeSymbol)
        {
            string keyword = namedTypeSymbol.DeclaringSyntaxReferences
                .OfType<TypeDeclarationSyntax>().FirstOrDefault()?
                .Keyword.Text;

            return keyword ?? namedTypeSymbol.TypeKind switch
            {
                TypeKind.Interface => "interface",
                TypeKind.Struct => "struct",
                _ => "class"
            };
        }

        /// <summary>
        /// 获取类的命名空间
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public static string GetClassNamespace(this INamedTypeSymbol symbol)
        {
            INamespaceSymbol namespaceSymbol = symbol.ContainingNamespace;
            string classNs = namespaceSymbol != null && !namespaceSymbol.IsGlobalNamespace ?
                namespaceSymbol.FullQualifiedNameOmitGlobal() :
                string.Empty;
            return classNs;
        }

        public static bool IsNested(this TypeDeclarationSyntax cds)
            => cds.Parent is TypeDeclarationSyntax;

        /// <summary>
        /// 类型声明语法 中的类是否是包含局部类 (partial 关键字)
        /// </summary>
        /// <param name="cds">类型声明语法</param>
        /// <returns></returns>
        public static bool IsPartial(this TypeDeclarationSyntax cds)
            => cds.Modifiers.Any(SyntaxKind.PartialKeyword);

        public static bool AreAllOuterTypesPartial(
            this TypeDeclarationSyntax cds,
            out TypeDeclarationSyntax typeMissingPartial
        )
        {
            SyntaxNode outerSyntaxNode = cds.Parent;

            while (outerSyntaxNode is TypeDeclarationSyntax outerTypeDeclSyntax)
            {
                if (!outerTypeDeclSyntax.IsPartial())
                {
                    typeMissingPartial = outerTypeDeclSyntax;
                    return false;
                }

                outerSyntaxNode = outerSyntaxNode.Parent;
            }

            typeMissingPartial = null;
            return true;
        }

        /// <summary>
        /// 带类型参数的名称
        /// </summary>
        /// <param name="symbol">类型符号</param>
        /// <returns>返回 TypeName&lt;TName&gt; 这样的字符串</returns>
        public static string NameWithTypeParameters(this INamedTypeSymbol symbol)
        {
            return symbol.IsGenericType ?
                string.Concat(symbol.Name, "<", string.Join(", ", symbol.TypeParameters), ">") :
                symbol.Name;
        }

        /// <summary>
        /// 获取符号显示格式, 忽略全局命名空间
        /// </summary>
        private static SymbolDisplayFormat FullyQualifiedFormatOmitGlobal { get; } =
            SymbolDisplayFormat.FullyQualifiedFormat
                .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);

        /// <summary>
        /// 获取符号显示格式, 包含全局命名空间
        /// </summary>
        private static SymbolDisplayFormat FullyQualifiedFormatIncludeGlobal { get; } =
            SymbolDisplayFormat.FullyQualifiedFormat
                .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included);

        /// <summary>
        /// 获取完整的限定名称, 并忽略全局命名空间
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public static string FullQualifiedNameOmitGlobal(this ITypeSymbol symbol)
            => symbol.ToDisplayString(NullableFlowState.NotNull, FullyQualifiedFormatOmitGlobal);

        /// <summary>
        /// 获取完整的限定名称, 并忽略全局命名空间
        /// </summary>
        /// <param name="namespaceSymbol"></param>
        /// <returns></returns>
        public static string FullQualifiedNameOmitGlobal(this INamespaceSymbol namespaceSymbol)
            => namespaceSymbol.ToDisplayString(FullyQualifiedFormatOmitGlobal);

        /// <summary>
        /// 获取完整的限定名称, 并包含全局命名空间
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public static string FullQualifiedNameIncludeGlobal(this ITypeSymbol symbol)
            => symbol.ToDisplayString(NullableFlowState.NotNull, FullyQualifiedFormatIncludeGlobal);

        /// <summary>
        /// 获取完整的限定名称, 并包含全局命名空间
        /// </summary>
        /// <param name="namespaceSymbol"></param>
        /// <returns></returns>
        public static string FullQualifiedNameIncludeGlobal(this INamespaceSymbol namespaceSymbol)
            => namespaceSymbol.ToDisplayString(FullyQualifiedFormatIncludeGlobal);

        /// <summary>
        /// 对限定名称特殊处理, 用于生成唯一文件名标识
        /// </summary>
        /// <param name="qualifiedName"></param>
        /// <returns></returns>
        public static string SanitizeQualifiedNameForUniqueHint(this string qualifiedName)
            => qualifiedName
                // 文件名称不能有特殊符号
                .Replace("<", "(Of ")
                .Replace(">", ")");

        /// <summary>
        /// 是否包含 Rpc 特性
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public static bool IsGodotRpcAttribute(this INamedTypeSymbol symbol)
            => symbol.FullQualifiedNameOmitGlobal() == ClassFullName.RpcAttr;

        /// <summary>
        /// 是否包含 Signal 特性
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public static bool IsGodotSignalAttribute(this INamedTypeSymbol symbol)
            => symbol.FullQualifiedNameOmitGlobal() == ClassFullName.SignalAttr;

        /// <summary>
        /// 是否包含 AutoGet 特性
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public static bool IsAutoGetAttribute(this INamedTypeSymbol symbol)
            => symbol.FullQualifiedNameOmitGlobal() == ClassFullName.AutoGetAttr;

        /// <summary>
        /// 蛇形命名(SnakeCase)转为帕斯卡命名(PascalCase) (godot_name = GodotName)
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string ToPascalCase(this string snakeCase)
        {
            string pattern = "(^|_)([a-zA-Z])";
            string pascalCase = Regex.Replace(snakeCase, pattern, match =>
            { // 将匹配的首字母转换为大写
                return match.Groups[2].Value.ToUpper();
            });

            return pascalCase;
        }
    }
}
