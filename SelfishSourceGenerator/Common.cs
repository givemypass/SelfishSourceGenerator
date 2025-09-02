using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SelfishSourceGenerator
{
    public static class Common
    {
        public static bool IsDerivedFrom(this INamedTypeSymbol baseType, string targetType)
        {
            while (baseType != null)
            {
                if (baseType.Name == targetType)
                    return true;

                baseType = baseType.BaseType;
            }

            return false;
        }

        public static bool IsImplementingInterface(this ITypeSymbol type, string @interface)
        {
            foreach (var implemented in type.AllInterfaces)
                if (implemented.Name == @interface)
                    return true;

            return false;
        }

        public static INamedTypeSymbol GetClassSymbol(GeneratorSyntaxContext context)
        {
            var candidate = Unsafe.As<ClassDeclarationSyntax>(context.Node);
            return ModelExtensions.GetDeclaredSymbol(context.SemanticModel, candidate) as INamedTypeSymbol;
        }

        public static INamedTypeSymbol GetStructSymbol(GeneratorSyntaxContext context)
        {
            var candidate = Unsafe.As<StructDeclarationSyntax>(context.Node);
            return ModelExtensions.GetDeclaredSymbol(context.SemanticModel, candidate) as INamedTypeSymbol;
        }

        public static bool IsNotAbstractClass(SyntaxNode node)
        {
            return node is ClassDeclarationSyntax classDeclaration &&
                   !classDeclaration.Modifiers.Any(SyntaxKind.AbstractKeyword);
        }

        public static bool IsStruct(SyntaxNode node)
        {
            return node is StructDeclarationSyntax;
        }

        public static string GetNamespaceName(INamedTypeSymbol classSymbol)
        {
            return classSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : $"namespace {classSymbol.ContainingNamespace.ToDisplayString()}";
        }
    }
}