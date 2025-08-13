using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SelfishSourceGenerator
{
    [Generator]
    public class SelfishSourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
            
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxContextReceiver is SyntaxReceiver receiver))
            {
                return;
            }

            foreach (var group in receiver.Fields
                         .GroupBy<IFieldSymbol, INamedTypeSymbol>(f => f.ContainingType,
                             SymbolEqualityComparer.Default))
            {
                var classSource = ProcessClass(group.Key, group);
                context.AddSource($"{group.Key.Name}_g.cs", SourceText.From(classSource, Encoding.UTF8));
            }
        }

        private string ProcessClass(INamedTypeSymbol classSymbol, IGrouping<INamedTypeSymbol, IFieldSymbol> fields)
        {
            var source = new StringBuilder($@"
using SelfishFramework.Src.Core;

namespace {classSymbol.ContainingNamespace.ToDisplayString()}
{{
    public partial class {classSymbol.Name}
    {{
        private void SetComponents()
    {{
");
            foreach (var field in fields)
            {
                var fieldName = field.Name;
                source.AppendLine($"Entity.Set({fieldName});");
            }

            source.AppendLine("       }");
            source.AppendLine("   }");
            source.AppendLine("}");
            return source.ToString();
        }
    }
    
    internal class SyntaxReceiver : ISyntaxContextReceiver
    {
        public List<IFieldSymbol> Fields { get; } = new List<IFieldSymbol>();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            if (context.Node is FieldDeclarationSyntax fieldDeclarationSyntax)
            {
                foreach (VariableDeclaratorSyntax variable in fieldDeclarationSyntax.Declaration.Variables)
                {
                    IFieldSymbol fieldSymbol = context.SemanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;

                    if (IsDerivedFrom(fieldSymbol?.ContainingType.BaseType, "Actor") &&
                        IsImplementingInterface(fieldSymbol?.Type, "IComponent"))
                    {
                        Fields.Add(fieldSymbol);
                    }
                }
            }
        }

        private bool IsDerivedFrom(INamedTypeSymbol baseType, string targetType)
        {
            while (baseType != null)
            {
                if (baseType.Name == targetType)
                    return true;

                baseType = baseType.BaseType;
            }

            return false;
        }
        
        private bool IsImplementingInterface(ITypeSymbol type, string @interface)
        {
            foreach (var implemented in type.AllInterfaces)
            {
                if (implemented.Name == @interface)
                {
                    return true;
                }        
            }

            return false;
        }
    }
}