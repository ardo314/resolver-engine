using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Engine.Generators;

/// <summary>
/// Serializable representation of a method on a behaviour interface.
/// </summary>
internal sealed class MethodInfo : IEquatable<MethodInfo>
{
    public string Name { get; }
    public string ReturnType { get; }
    public bool ReturnsVoidTask { get; }
    public ImmutableArray<ParameterInfo> Parameters { get; }

    public MethodInfo(
        string name,
        string returnType,
        bool returnsVoidTask,
        ImmutableArray<ParameterInfo> parameters
    )
    {
        Name = name;
        ReturnType = returnType;
        ReturnsVoidTask = returnsVoidTask;
        Parameters = parameters;
    }

    public static MethodInfo From(IMethodSymbol method)
    {
        var returnType = method.ReturnType.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat
        );
        var returnsVoidTask = returnType == "global::System.Threading.Tasks.Task";

        var parameters = method
            .Parameters.Select(p => new ParameterInfo(
                p.Name,
                p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    == "global::System.Threading.CancellationToken"
            ))
            .ToImmutableArray();

        return new MethodInfo(method.Name, returnType, returnsVoidTask, parameters);
    }

    public bool Equals(MethodInfo? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return Name == other.Name
            && ReturnType == other.ReturnType
            && ReturnsVoidTask == other.ReturnsVoidTask
            && Parameters.SequenceEqual(other.Parameters);
    }

    public override bool Equals(object? obj) => Equals(obj as MethodInfo);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = Name.GetHashCode();
            hash = hash * 31 + ReturnType.GetHashCode();
            hash = hash * 31 + ReturnsVoidTask.GetHashCode();
            foreach (var p in Parameters)
                hash = hash * 31 + p.GetHashCode();
            return hash;
        }
    }
}

/// <summary>
/// Serializable representation of a method parameter.
/// </summary>
internal sealed class ParameterInfo : IEquatable<ParameterInfo>
{
    public string Name { get; }
    public string Type { get; }
    public bool IsCancellationToken { get; }

    public ParameterInfo(string name, string type, bool isCancellationToken)
    {
        Name = name;
        Type = type;
        IsCancellationToken = isCancellationToken;
    }

    public bool Equals(ParameterInfo? other)
    {
        if (other is null)
            return false;
        return Name == other.Name
            && Type == other.Type
            && IsCancellationToken == other.IsCancellationToken;
    }

    public override bool Equals(object? obj) => Equals(obj as ParameterInfo);

    public override int GetHashCode()
    {
        unchecked
        {
            return Name.GetHashCode() * 31 + Type.GetHashCode();
        }
    }
}

/// <summary>
/// Info about an <c>IBehaviour</c>-derived interface for client proxy generation.
/// </summary>
internal sealed class BehaviourInterfaceInfo : IEquatable<BehaviourInterfaceInfo>
{
    public string InterfaceName { get; }
    public string FullyQualifiedName { get; }
    public string? Namespace { get; }
    public ImmutableArray<MethodInfo> Methods { get; }

    public BehaviourInterfaceInfo(
        string interfaceName,
        string fullyQualifiedName,
        string? ns,
        ImmutableArray<MethodInfo> methods
    )
    {
        InterfaceName = interfaceName;
        FullyQualifiedName = fullyQualifiedName;
        Namespace = ns;
        Methods = methods;
    }

    public static BehaviourInterfaceInfo From(INamedTypeSymbol symbol)
    {
        var methods = symbol.GetDeclaredMethods().Select(MethodInfo.From).ToImmutableArray();

        return new BehaviourInterfaceInfo(
            symbol.Name,
            symbol.FullyQualifiedName(),
            symbol.GetNamespaceName(),
            methods
        );
    }

    public string ProxyClassName
    {
        get
        {
            var name = InterfaceName;
            if (name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]))
                name = name.Substring(1);
            return name + "Proxy";
        }
    }

    public bool Equals(BehaviourInterfaceInfo? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return FullyQualifiedName == other.FullyQualifiedName
            && Methods.SequenceEqual(other.Methods);
    }

    public override bool Equals(object? obj) => Equals(obj as BehaviourInterfaceInfo);

    public override int GetHashCode() => FullyQualifiedName.GetHashCode();
}

/// <summary>
/// Info about a concrete class implementing an <c>IBehaviour</c> contract for server stub generation.
/// </summary>
internal sealed class BehaviourImplInfo : IEquatable<BehaviourImplInfo>
{
    public string ClassName { get; }
    public string FullyQualifiedClassName { get; }
    public string? Namespace { get; }
    public ImmutableArray<BehaviourContractInfo> Contracts { get; }

    public BehaviourImplInfo(
        string className,
        string fullyQualifiedClassName,
        string? ns,
        ImmutableArray<BehaviourContractInfo> contracts
    )
    {
        ClassName = className;
        FullyQualifiedClassName = fullyQualifiedClassName;
        Namespace = ns;
        Contracts = contracts;
    }

    public static BehaviourImplInfo From(INamedTypeSymbol symbol)
    {
        var contracts = symbol
            .GetBehaviourContracts()
            .Select(i => new BehaviourContractInfo(
                i.Name,
                i.FullyQualifiedName(),
                i.GetDeclaredMethods().Select(MethodInfo.From).ToImmutableArray()
            ))
            .ToImmutableArray();

        return new BehaviourImplInfo(
            symbol.Name,
            symbol.FullyQualifiedName(),
            symbol.GetNamespaceName(),
            contracts
        );
    }

    public string DispatcherClassName => ClassName + "Dispatcher";

    public bool Equals(BehaviourImplInfo? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return FullyQualifiedClassName == other.FullyQualifiedClassName
            && Contracts.SequenceEqual(other.Contracts);
    }

    public override bool Equals(object? obj) => Equals(obj as BehaviourImplInfo);

    public override int GetHashCode() => FullyQualifiedClassName.GetHashCode();
}

/// <summary>
/// Info about a single behaviour contract interface implemented by a class.
/// </summary>
internal sealed class BehaviourContractInfo : IEquatable<BehaviourContractInfo>
{
    public string InterfaceName { get; }
    public string FullyQualifiedName { get; }
    public ImmutableArray<MethodInfo> Methods { get; }

    public BehaviourContractInfo(
        string interfaceName,
        string fullyQualifiedName,
        ImmutableArray<MethodInfo> methods
    )
    {
        InterfaceName = interfaceName;
        FullyQualifiedName = fullyQualifiedName;
        Methods = methods;
    }

    public bool Equals(BehaviourContractInfo? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return FullyQualifiedName == other.FullyQualifiedName
            && Methods.SequenceEqual(other.Methods);
    }

    public override bool Equals(object? obj) => Equals(obj as BehaviourContractInfo);

    public override int GetHashCode() => FullyQualifiedName.GetHashCode();
}

/// <summary>
/// Info about an <c>IComponent</c> implementation for MessagePack resolver generation.
/// </summary>
internal sealed class ComponentInfo : IEquatable<ComponentInfo>
{
    public string TypeName { get; }
    public string FullyQualifiedName { get; }
    public string? Namespace { get; }

    public ComponentInfo(string typeName, string fullyQualifiedName, string? ns)
    {
        TypeName = typeName;
        FullyQualifiedName = fullyQualifiedName;
        Namespace = ns;
    }

    public static ComponentInfo From(INamedTypeSymbol symbol)
    {
        return new ComponentInfo(
            symbol.Name,
            symbol.FullyQualifiedName(),
            symbol.GetNamespaceName()
        );
    }

    public bool Equals(ComponentInfo? other)
    {
        if (other is null)
            return false;
        return FullyQualifiedName == other.FullyQualifiedName;
    }

    public override bool Equals(object? obj) => Equals(obj as ComponentInfo);

    public override int GetHashCode() => FullyQualifiedName.GetHashCode();
}

/// <summary>
/// Info about a <c>Component&lt;TContract&gt;</c> subclass for partial class generation.
/// The generator emits event backing fields, raise methods, and the contract interface marker.
/// </summary>
internal sealed class ComponentImplInfo : IEquatable<ComponentImplInfo>
{
    /// <summary>The concrete class name (e.g., "InMemoryPose").</summary>
    public string ClassName { get; }

    /// <summary>Fully qualified name with global:: prefix.</summary>
    public string FullyQualifiedClassName { get; }

    /// <summary>Namespace of the class, or null if global.</summary>
    public string? Namespace { get; }

    /// <summary>The contract interface name (e.g., "IPose").</summary>
    public string ContractInterfaceName { get; }

    /// <summary>Fully qualified contract interface name.</summary>
    public string FullyQualifiedContractName { get; }

    /// <summary>Fully qualified data type name (e.g., "global::Example.Pose").</summary>
    public string FullyQualifiedDataType { get; }

    /// <summary>Short data type name (e.g., "Pose").</summary>
    public string DataTypeName { get; }

    public ComponentImplInfo(
        string className,
        string fullyQualifiedClassName,
        string? ns,
        string contractInterfaceName,
        string fullyQualifiedContractName,
        string dataTypeName,
        string fullyQualifiedDataType
    )
    {
        ClassName = className;
        FullyQualifiedClassName = fullyQualifiedClassName;
        Namespace = ns;
        ContractInterfaceName = contractInterfaceName;
        FullyQualifiedContractName = fullyQualifiedContractName;
        DataTypeName = dataTypeName;
        FullyQualifiedDataType = fullyQualifiedDataType;
    }

    public static ComponentImplInfo? From(INamedTypeSymbol symbol)
    {
        var contractType = symbol.GetComponentContractType();
        if (contractType is null)
            return null;

        var dataType = contractType.GetComponentDataType();
        if (dataType is null)
            return null;

        return new ComponentImplInfo(
            symbol.Name,
            symbol.FullyQualifiedName(),
            symbol.GetNamespaceName(),
            contractType.Name,
            contractType.FullyQualifiedName(),
            dataType.Name,
            dataType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
        );
    }

    public bool Equals(ComponentImplInfo? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return FullyQualifiedClassName == other.FullyQualifiedClassName
            && FullyQualifiedContractName == other.FullyQualifiedContractName
            && FullyQualifiedDataType == other.FullyQualifiedDataType;
    }

    public override bool Equals(object? obj) => Equals(obj as ComponentImplInfo);

    public override int GetHashCode() => FullyQualifiedClassName.GetHashCode();
}
