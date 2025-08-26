using Microsoft.CodeAnalysis;

namespace SelfishSourceGenerator
{
    public static class Helpers
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
    }
}