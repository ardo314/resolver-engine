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
    private const string ComponentGenericFqn = "Engine.Core.IComponent<";
    private const string ComponentBaseFqn = "Engine.Core.Component<";

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
    /// Used for MessagePack resolver generation — matches the component <b>data</b> types
    /// (structs/classes implementing <c>IComponent</c>), not <c>ComponentBase</c> subclasses.
    /// </summary>
    public static bool IsComponentImplementation(this INamedTypeSymbol symbol)
    {
        if (symbol.TypeKind != TypeKind.Struct && symbol.TypeKind != TypeKind.Class)
            return false;

        if (symbol.IsAbstract)
            return false;

        // Exclude Component subclasses — those are component implementations, not data types
        if (symbol.IsComponentSubclass())
            return false;

        return symbol.AllInterfaces.Any(i =>
            i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            == "global::" + ComponentInterfaceFqn
        );
    }

    /// <summary>
    /// Returns true if the type is a concrete (non-abstract, partial) class
    /// that derives from <c>Engine.Core.Component&lt;TContract&gt;</c>.
    /// </summary>
    public static bool IsComponentSubclass(this INamedTypeSymbol symbol)
    {
        if (symbol.TypeKind != TypeKind.Class || symbol.IsAbstract)
            return false;

        var baseType = symbol.BaseType;
        while (baseType is not null)
        {
            var display = baseType.OriginalDefinition.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            );
            if (display == "global::Engine.Core.Component<TContract>")
                return true;
            baseType = baseType.BaseType;
        }

        return false;
    }

    /// <summary>
    /// Extracts the contract interface type parameter from a <c>Component&lt;TContract&gt;</c> subclass.
    /// Returns null if the symbol is not a Component subclass.
    /// </summary>
    public static INamedTypeSymbol? GetComponentContractType(this INamedTypeSymbol symbol)
    {
        var baseType = symbol.BaseType;
        while (baseType is not null)
        {
            var display = baseType.OriginalDefinition.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            );
            if (display == "global::Engine.Core.Component<TContract>")
            {
                return baseType.TypeArguments[0] as INamedTypeSymbol;
            }
            baseType = baseType.BaseType;
        }

        return null;
    }

    /// <summary>
    /// Extracts TData from a contract interface that extends <c>IComponent&lt;TData&gt;</c>.
    /// Returns null if the interface doesn't extend IComponent&lt;TData&gt;.
    /// </summary>
    public static ITypeSymbol? GetComponentDataType(this INamedTypeSymbol contractInterface)
    {
        // Check the interface itself
        foreach (var iface in contractInterface.AllInterfaces)
        {
            if (
                iface.IsGenericType
                && iface.OriginalDefinition.ToDisplayString(
                    SymbolDisplayFormat.FullyQualifiedFormat
                ) == "global::Engine.Core.IComponent<TData>"
            )
            {
                return iface.TypeArguments[0];
            }
        }

        // Also check if the contract IS IComponent<TData> directly
        if (
            contractInterface.IsGenericType
            && contractInterface.OriginalDefinition.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            ) == "global::Engine.Core.IComponent<TData>"
        )
        {
            return contractInterface.TypeArguments[0];
        }

        return null;
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
