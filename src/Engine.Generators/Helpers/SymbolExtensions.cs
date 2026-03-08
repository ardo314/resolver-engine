using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Engine.Generators;

/// <summary>
/// Extension methods for working with Roslyn symbols.
/// </summary>
internal static class SymbolExtensions
{
    private const string BehaviourInterfaceFqn = "Engine.Core.IBehaviour";
    private const string ComponentInterfaceFqn = "Engine.Core.IComponent";

    /// <summary>
    /// Returns true if the symbol is an interface that directly extends <c>Engine.Core.IBehaviour</c>.
    /// </summary>
    public static bool IsBehaviourInterface(this INamedTypeSymbol symbol)
    {
        if (symbol.TypeKind != TypeKind.Interface)
            return false;

        return symbol.AllInterfaces.Any(i =>
            i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            == "global::" + BehaviourInterfaceFqn
        );
    }

    /// <summary>
    /// Returns true if the type implements <c>Engine.Core.IComponent</c> and is a concrete (non-abstract) type.
    /// </summary>
    public static bool IsComponentImplementation(this INamedTypeSymbol symbol)
    {
        if (symbol.TypeKind != TypeKind.Struct && symbol.TypeKind != TypeKind.Class)
            return false;

        if (symbol.IsAbstract)
            return false;

        return symbol.AllInterfaces.Any(i =>
            i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            == "global::" + ComponentInterfaceFqn
        );
    }

    /// <summary>
    /// Returns true if the type is a concrete class that implements a <c>IBehaviour</c>-derived interface.
    /// </summary>
    public static bool IsBehaviourImplementation(this INamedTypeSymbol symbol)
    {
        if (symbol.TypeKind != TypeKind.Class || symbol.IsAbstract)
            return false;

        return symbol.AllInterfaces.Any(i => i.IsBehaviourInterface());
    }

    /// <summary>
    /// Gets the <c>IBehaviour</c>-derived interfaces that a concrete class implements.
    /// Excludes <c>IBehaviour</c> itself.
    /// </summary>
    public static IEnumerable<INamedTypeSymbol> GetBehaviourContracts(this INamedTypeSymbol symbol)
    {
        return symbol.AllInterfaces.Where(i => i.IsBehaviourInterface());
    }

    /// <summary>
    /// Returns the fully-qualified name suitable for use in generated source (with global:: prefix).
    /// </summary>
    public static string FullyQualifiedName(this INamedTypeSymbol symbol)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    /// <summary>
    /// Returns the namespace of the symbol as a string, or null if it's the global namespace.
    /// </summary>
    public static string? GetNamespaceName(this INamedTypeSymbol symbol)
    {
        var ns = symbol.ContainingNamespace;
        if (ns is null || ns.IsGlobalNamespace)
            return null;
        return ns.ToDisplayString();
    }

    /// <summary>
    /// Get the declared methods on an interface (not inherited).
    /// </summary>
    public static IEnumerable<IMethodSymbol> GetDeclaredMethods(
        this INamedTypeSymbol interfaceSymbol
    )
    {
        return interfaceSymbol
            .GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary);
    }
}
