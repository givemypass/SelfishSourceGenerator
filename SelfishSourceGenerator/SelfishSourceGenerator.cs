using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SelfishSourceGenerator
{
    [Generator(LanguageNames.CSharp)]
    public class SelfishSourceGenerator : IIncrementalGenerator
    {
        private const string ACTOR_TYPE_NAME = "Actor";
        private const string COMPONENT_INTERFACE = "IComponent";
        private const string SYSTEM_INTERFACE = "ISystem";
        private const string PROVIDER_COMPONENT_ATTRIBUTE = "ProviderComponent";
        private const string REACT_LOCAL_COMMAND_INTERFACE = "IReactLocal";
        private const string REACT_GLOBAL_COMMAND_INTERFACE = "IReactGlobal";
        private const string IDENTIFIER_CONTAINER = "IdentifierContainer";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            ActorPipeline(context);
            ReactCommandsPipeline(context);
            // context.SyntaxProvider.CreateSyntaxProvider(IsNotAbstractClass, GetClassSymbol)
            //     .Where(a => a != null)
            //     .Select(((symbol, token) =>
            //     {
            //         token.ThrowIfCancellationRequested();
            //         if (!symbol.IsDerivedFrom(IDENTIFIER_CONTAINER))
            //         {
            //             return null;
            //         }
            //     })
        }

        private void ReactCommandsPipeline(IncrementalGeneratorInitializationContext context)
        {
            var reactCommandsPipeline = context.SyntaxProvider
                .CreateSyntaxProvider(IsNotAbstractClass, GetClassSymbol)
                .Where(a => a != null)
                .Select((symbol, token) =>
                {
                    token.ThrowIfCancellationRequested();

                    if (!symbol.IsImplementingInterface(SYSTEM_INTERFACE))
                    {
                        return null;
                    }

                    string[] localCommands = Array.Empty<string>();
                    string[] globalCommands = Array.Empty<string>();
                    if (symbol.IsImplementingInterface(REACT_LOCAL_COMMAND_INTERFACE))
                    {
                        localCommands = symbol.AllInterfaces
                            .Where(i => i.Name == REACT_LOCAL_COMMAND_INTERFACE && i.IsGenericType)
                            .Select(i => i.TypeArguments.First())
                            .OfType<INamedTypeSymbol>()
                            .Select(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                            .ToArray();
                    }
                    
                    if (symbol.IsImplementingInterface(REACT_GLOBAL_COMMAND_INTERFACE))
                    {
                        globalCommands = symbol.AllInterfaces
                            .Where(i => i.Name == REACT_GLOBAL_COMMAND_INTERFACE && i.IsGenericType)
                            .Select(i => i.TypeArguments.First())
                            .OfType<INamedTypeSymbol>()
                            .Select(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                            .ToArray();
                    }

                    return new
                    {
                        ClassSymbol = symbol,
                        LocalCommands = localCommands,
                        GlobalCommands = globalCommands,
                    };
                })
                .Where(a => a != null);
            context.RegisterSourceOutput(reactCommandsPipeline, (productionContext, source) => GenerateLocalCommandListener(productionContext, source.ClassSymbol, source.LocalCommands, source.GlobalCommands));
        }

        private void GenerateLocalCommandListener(SourceProductionContext context, INamedTypeSymbol classSymbol, string[] localCommands, string[] globalCommands)
        {
            var namespaceName = GetNamespaceName(classSymbol);
            var registerMethod = new StringBuilder();
            registerMethod.AppendLine("    public override void RegisterCommands()");
            registerMethod.AppendLine("    {");
            foreach (var command in localCommands)
                registerMethod.AppendLine($"        Owner.GetWorld().ModuleRegistry.GetModule<LocalCommandModule>().Register<{command}>(Owner, this);");
            foreach (var command in globalCommands)
            {
                registerMethod.AppendLine($"        Owner.GetWorld().ModuleRegistry.GetModule<GlobalCommandModule>().Register<{command}>(this);");
            }
            registerMethod.AppendLine("    }");
            
            var unregisterMethod = new StringBuilder();
            unregisterMethod.AppendLine("    public override void UnregisterCommands()");
            unregisterMethod.AppendLine("    {");
            foreach (var command in localCommands)
                unregisterMethod.AppendLine($"        Owner.GetWorld().ModuleRegistry.GetModule<LocalCommandModule>().Unregister<{command}>(Owner, this);");
            foreach (var command in globalCommands)
            {
                unregisterMethod.AppendLine($"        Owner.GetWorld().ModuleRegistry.GetModule<GlobalCommandModule>().Unregister<{command}>(this);");
            }
            unregisterMethod.AppendLine("    }");
            
            var code = $@"
// <auto-generated/>
#pragma warning disable
#nullable enable
using SelfishFramework.Src.Core;
using SelfishFramework.Src.Core.SystemModules.CommandBusModule;


{namespaceName}
{{
    public partial class {classSymbol.Name}
    {{
    {registerMethod}

    {unregisterMethod}
    }}
}}
    ";
            var className = classSymbol.Name;
            context.AddSource($"{className}_commands.g.cs", code);
        }

        private static void ActorPipeline(IncrementalGeneratorInitializationContext context)
        {
            var actorPipeline = context.SyntaxProvider
                .CreateSyntaxProvider(IsNotAbstractClass, GetClassSymbol)
                .Where(a => a != null)
                .Select((syntaxContext, token) =>
                {
                    token.ThrowIfCancellationRequested();

                    if (!syntaxContext.BaseType.IsDerivedFrom(ACTOR_TYPE_NAME))
                    {
                        return null;
                    }

                    var componentFields = new List<string>();
                    foreach (var member in syntaxContext.GetMembers())
                        if (member is IFieldSymbol fieldSymbol)
                        {
                            if (fieldSymbol.Type.IsImplementingInterface(COMPONENT_INTERFACE))
                                componentFields.Add(fieldSymbol.Name);
                        }

                    return new
                    {
                        ClassSymbol = syntaxContext,
                        ComponentFields = componentFields,
                    };
                })
                .Where(a => a != null);

            context.RegisterSourceOutput(actorPipeline,
                (productionContext, source) => ProcessActor(productionContext, source.ClassSymbol, source.ComponentFields));
        }

        private static void ProcessActor(SourceProductionContext productionContext, INamedTypeSymbol classSymbol,
            List<string> componentFields)
        {
            var namespaceName = GetNamespaceName(classSymbol);

            var componentMethodBody = new StringBuilder();
            componentMethodBody.AppendLine("    protected override void SetComponents()");
            componentMethodBody.AppendLine("    {");
            componentMethodBody.AppendLine("        base.SetComponents();");
            foreach (var field in componentFields)
                componentMethodBody.AppendLine($"        Entity.Set({field});");
            componentMethodBody.AppendLine("    }");

            var code = $@"
// <auto-generated/>
#pragma warning disable
#nullable enable
using SelfishFramework.Src.Core;

{namespaceName}
{{
    public partial class {classSymbol.Name}
    {{
    {componentMethodBody}
    }}
}}
    ";
            var className = classSymbol.Name;
            productionContext.AddSource($"{className}.g.cs", code);
        }

        private static string GetNamespaceName(INamedTypeSymbol classSymbol)
        {
            return classSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : $"namespace {classSymbol.ContainingNamespace.ToDisplayString()}";
        }


        private static INamedTypeSymbol GetClassSymbol(GeneratorSyntaxContext context, CancellationToken token)
        {
            var candidate = Unsafe.As<ClassDeclarationSyntax>(context.Node);
            return ModelExtensions.GetDeclaredSymbol(context.SemanticModel, candidate) as INamedTypeSymbol;
        }
        
        private static INamedTypeSymbol GetStructSymbol(GeneratorSyntaxContext context, CancellationToken token)
        {
            var candidate = Unsafe.As<StructDeclarationSyntax>(context.Node);
            return ModelExtensions.GetDeclaredSymbol(context.SemanticModel, candidate) as INamedTypeSymbol;
        }

        private static bool IsNotAbstractClass(SyntaxNode node, CancellationToken token)
        {
            return node is ClassDeclarationSyntax classDeclaration &&
                   !classDeclaration.Modifiers.Any(SyntaxKind.AbstractKeyword);
        }
        
        private static bool IsStruct(SyntaxNode node, CancellationToken token)
        {
            return node is StructDeclarationSyntax;
        }
    }
}