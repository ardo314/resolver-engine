using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Engine.Generators;

[Generator]
public sealed class ComponentProxyGenerator : IIncrementalGenerator
{
    // Fully qualified names we look for in the compilation.
    private const string IComponentFqn = "Engine.Client.IComponent";
    private const string IBehaviourFqn = "Engine.Client.IBehaviour";
    private const string ComponentWorkerFqn = "Engine.Module.ComponentWorker";
    private const string HasAttributeFqn = "Engine.Client.HasAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // ── Worker-side: find all classes that inherit ComponentWorker<T> ──

        var workerDeclarations = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax cds
                    && cds.Modifiers.Any(SyntaxKind.PartialKeyword),
                transform: static (ctx, ct) =>
                {
                    var symbol =
                        ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) as INamedTypeSymbol;
                    return symbol;
                }
            )
            .Where(static s => s is not null)!;

        var workerInfos = workerDeclarations
            .Combine(context.CompilationProvider)
            .Select(
                (pair, ct) =>
                {
                    var (symbol, compilation) = pair;
                    if (symbol is null)
                        return default;

                    var componentWorkerType = compilation.GetTypeByMetadataName(
                        $"{ComponentWorkerFqn}`1"
                    );
                    if (componentWorkerType is null)
                        return default;

                    var markerStruct = GetMarkerStructArgument(symbol!, componentWorkerType);
                    if (markerStruct is null)
                        return default;

                    // Read [Has<>] attributes from the marker struct to get component interfaces
                    var componentInterfaces = GetComponentInterfaces(markerStruct, compilation);
                    if (componentInterfaces.Length == 0)
                        return default;

                    var allComponents = ImmutableArray.CreateBuilder<ComponentInfo>();
                    foreach (var iface in componentInterfaces)
                    {
                        var methods = CollectComponentMethods(iface, compilation);
                        if (methods.Length == 0)
                            continue;

                        allComponents.Add(
                            new ComponentInfo(
                                iface.Name,
                                iface.ToDisplayString(),
                                iface.ContainingNamespace.IsGlobalNamespace
                                    ? ""
                                    : iface.ContainingNamespace.ToDisplayString(),
                                methods
                            )
                        );
                    }

                    if (allComponents.Count == 0)
                        return default;

                    return new WorkerInfo(
                        symbol!.ToDisplayString(),
                        symbol.Name,
                        symbol.ContainingNamespace.IsGlobalNamespace
                            ? ""
                            : symbol.ContainingNamespace.ToDisplayString(),
                        markerStruct.Name,
                        markerStruct.ToDisplayString(),
                        markerStruct.ContainingNamespace.IsGlobalNamespace
                            ? ""
                            : markerStruct.ContainingNamespace.ToDisplayString(),
                        allComponents.ToImmutable()
                    );
                }
            )
            .Where(static w => w.WorkerName is not null);

        context.RegisterSourceOutput(
            workerInfos,
            static (spc, info) =>
            {
                var source = GenerateWorkerPartial(info);
                spc.AddSource($"{info.WorkerName}.g.cs", source);
            }
        );

        // ── Client-side: derive behaviour proxies from worker marker structs ──
        // Only generates proxies for behaviour interfaces referenced by [Has<>]
        // on component structs that have a local ComponentWorker<T> declaration.

        var behaviourProxyInfos = workerInfos.SelectMany(
            (info, ct) =>
            {
                var results = ImmutableArray.CreateBuilder<ComponentInterfaceInfo>();
                foreach (var comp in info.Components)
                {
                    var proxyName =
                        comp.ComponentName.StartsWith("I")
                        && comp.ComponentName.Length > 1
                        && char.IsUpper(comp.ComponentName[1])
                            ? comp.ComponentName.Substring(1) + "Proxy"
                            : comp.ComponentName + "Proxy";

                    results.Add(
                        new ComponentInterfaceInfo(
                            comp.ComponentName,
                            comp.ComponentFullName,
                            comp.ComponentNamespace,
                            proxyName,
                            comp.Methods
                        )
                    );
                }
                return results.ToImmutable();
            }
        );

        context.RegisterSourceOutput(
            behaviourProxyInfos,
            static (spc, info) =>
            {
                var source = GenerateClientProxy(info);
                spc.AddSource($"{info.ProxyName}.g.cs", source);
            }
        );

        // ── Component-side: derive component proxies from worker marker structs ──
        // Only generates proxies for component structs that have a local ComponentWorker<T>.

        var componentProxyInfos = workerInfos
            .Select(
                (info, ct) =>
                {
                    var behaviours = ImmutableArray.CreateBuilder<ComponentInterfaceInfo>();
                    foreach (var comp in info.Components)
                    {
                        var proxyName =
                            comp.ComponentName.StartsWith("I")
                            && comp.ComponentName.Length > 1
                            && char.IsUpper(comp.ComponentName[1])
                                ? comp.ComponentName.Substring(1) + "Proxy"
                                : comp.ComponentName + "Proxy";

                        behaviours.Add(
                            new ComponentInterfaceInfo(
                                comp.ComponentName,
                                comp.ComponentFullName,
                                comp.ComponentNamespace,
                                proxyName,
                                comp.Methods
                            )
                        );
                    }

                    return new ComponentStructInfo(
                        info.MarkerStructName,
                        info.MarkerStructFullName,
                        info.MarkerStructNamespace,
                        info.MarkerStructName + "Proxy",
                        behaviours.ToImmutable()
                    );
                }
            )
            .Where(static info => info.StructName is not null);

        context.RegisterSourceOutput(
            componentProxyInfos,
            static (spc, info) =>
            {
                var source = GenerateComponentProxy(info);
                spc.AddSource($"{info.ProxyName}.g.cs", source);
            }
        );
    }

    // ── Symbol helpers ──────────────────────────────────────────────────

    private static INamedTypeSymbol? GetMarkerStructArgument(
        INamedTypeSymbol workerType,
        INamedTypeSymbol componentWorkerOpen
    )
    {
        var current = workerType.BaseType;
        while (current is not null)
        {
            if (
                current.IsGenericType
                && SymbolEqualityComparer.Default.Equals(
                    current.OriginalDefinition,
                    componentWorkerOpen
                )
            )
            {
                return current.TypeArguments[0] as INamedTypeSymbol;
            }
            current = current.BaseType;
        }
        return null;
    }

    /// <summary>
    /// Reads [Has&lt;T&gt;] attributes from a marker struct and returns the component interface types.
    /// </summary>
    private static ImmutableArray<INamedTypeSymbol> GetComponentInterfaces(
        INamedTypeSymbol markerStruct,
        Compilation compilation
    )
    {
        var hasAttributeType = compilation.GetTypeByMetadataName($"{HasAttributeFqn}`1");
        if (hasAttributeType is null)
            return ImmutableArray<INamedTypeSymbol>.Empty;

        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        foreach (var attr in markerStruct.GetAttributes())
        {
            if (attr.AttributeClass is null || !attr.AttributeClass.IsGenericType)
                continue;

            if (
                !SymbolEqualityComparer.Default.Equals(
                    attr.AttributeClass.OriginalDefinition,
                    hasAttributeType
                )
            )
                continue;

            if (attr.AttributeClass.TypeArguments[0] is INamedTypeSymbol componentInterface)
            {
                builder.Add(componentInterface);
            }
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<MethodInfo> CollectComponentMethods(
        INamedTypeSymbol componentInterface,
        Compilation compilation
    )
    {
        var iBehaviour = compilation.GetTypeByMetadataName(IBehaviourFqn);
        var builder = ImmutableArray.CreateBuilder<MethodInfo>();
        var seen = new HashSet<string>();

        CollectMethodsFromInterface(componentInterface, iBehaviour, builder, seen);

        return builder.ToImmutable();
    }

    private static void CollectMethodsFromInterface(
        INamedTypeSymbol iface,
        INamedTypeSymbol? iBehaviour,
        ImmutableArray<MethodInfo>.Builder builder,
        HashSet<string> seen
    )
    {
        // Skip IBehaviour itself — it's a marker with no methods.
        if (iBehaviour is not null && SymbolEqualityComparer.Default.Equals(iface, iBehaviour))
            return;

        foreach (var member in iface.GetMembers())
        {
            if (member is not IMethodSymbol method)
                continue;
            if (method.MethodKind != MethodKind.Ordinary)
                continue;

            // Deduplicate by name (handles diamond inheritance).
            if (!seen.Add(method.Name))
                continue;

            // Determine return type info.
            var isTask = IsTask(method.ReturnType, out var returnDataType);
            if (!isTask)
                continue; // Skip non-Task methods.

            // Determine parameter info (0 or 1 value parameter + optional CancellationToken).
            string? paramType = null;
            string? paramName = null;
            foreach (var p in method.Parameters)
            {
                if (p.Type.ToDisplayString() == "System.Threading.CancellationToken")
                    continue;
                if (paramType is not null)
                    goto NextMethod; // More than 1 value param — skip.
                paramType = p.Type.ToDisplayString();
                paramName = p.Name;
            }

            builder.Add(
                new MethodInfo(
                    method.Name,
                    returnDataType,
                    paramType,
                    paramName ?? "data",
                    iface.ToDisplayString()
                )
            );

            NextMethod:
            ;
        }

        // Recurse into base interfaces.
        foreach (var baseIface in iface.Interfaces)
        {
            CollectMethodsFromInterface(baseIface, iBehaviour, builder, seen);
        }
    }

    private static bool IsTask(ITypeSymbol type, out string? dataType)
    {
        dataType = null;

        if (type is INamedTypeSymbol named)
        {
            var fullName = named.OriginalDefinition.ToDisplayString();
            if (fullName == "System.Threading.Tasks.Task")
                return true;
            if (fullName == "System.Threading.Tasks.Task<TResult>")
            {
                dataType = named.TypeArguments[0].ToDisplayString();
                return true;
            }
        }
        return false;
    }

    private static bool ImplementsInterface(INamedTypeSymbol type, INamedTypeSymbol target)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, target))
                return true;
        }
        // Also check direct identity
        if (SymbolEqualityComparer.Default.Equals(type, target))
            return true;
        return false;
    }

    // ── Worker-side code generation ─────────────────────────────────────

    private static string GenerateWorkerPartial(WorkerInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Engine.Module;");
        sb.AppendLine("using MessagePack;");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(info.WorkerNamespace))
        {
            sb.AppendLine($"namespace {info.WorkerNamespace};");
            sb.AppendLine();
        }

        // Build the interface list from all components
        var interfaceList = string.Join(
            ", ",
            info.Components.Select(c => c.ComponentFullName).Append("IDataDispatch")
        );

        sb.AppendLine($"partial class {info.WorkerName} : {interfaceList}");
        sb.AppendLine("{");

        // Generate DispatchAsync method with component name disambiguation
        sb.AppendLine(
            "    public async Task<ReadOnlyMemory<byte>> DispatchAsync(string componentName, string methodName, ReadOnlyMemory<byte> payload, CancellationToken ct)"
        );
        sb.AppendLine("    {");
        sb.AppendLine("        switch (componentName)");
        sb.AppendLine("        {");

        foreach (var component in info.Components)
        {
            sb.AppendLine($"            case \"{component.ComponentName}\":");
            sb.AppendLine("                switch (methodName)");
            sb.AppendLine("                {");

            foreach (var method in component.Methods)
            {
                sb.AppendLine($"                    case \"{method.Name}\":");
                sb.AppendLine("                    {");

                // Use interface cast for disambiguation
                var cast = $"(({component.ComponentFullName})this)";

                if (method.ReturnDataType is not null)
                {
                    // Method returns Task<T> — call and serialize result.
                    if (method.ParamType is not null)
                    {
                        sb.AppendLine(
                            $"                        var param = MessagePackSerializer.Deserialize<{method.ParamType}>(payload, cancellationToken: ct);"
                        );
                        sb.AppendLine(
                            $"                        var result = await {cast}.{method.Name}(param, ct);"
                        );
                    }
                    else
                    {
                        sb.AppendLine(
                            $"                        var result = await {cast}.{method.Name}(ct);"
                        );
                    }
                    sb.AppendLine(
                        $"                        return MessagePackSerializer.Serialize(result, cancellationToken: ct);"
                    );
                }
                else
                {
                    // Method returns Task — call and return empty.
                    if (method.ParamType is not null)
                    {
                        sb.AppendLine(
                            $"                        var param = MessagePackSerializer.Deserialize<{method.ParamType}>(payload, cancellationToken: ct);"
                        );
                        sb.AppendLine(
                            $"                        await {cast}.{method.Name}(param, ct);"
                        );
                    }
                    else
                    {
                        sb.AppendLine($"                        await {cast}.{method.Name}(ct);");
                    }
                    sb.AppendLine("                        return ReadOnlyMemory<byte>.Empty;");
                }

                sb.AppendLine("                    }");
            }

            sb.AppendLine("                    default:");
            sb.AppendLine(
                $"                        throw new NotSupportedException($\"Unknown method '{{methodName}}' on component '{component.ComponentName}'.\");"
            );
            sb.AppendLine("                }");
        }

        sb.AppendLine("            default:");
        sb.AppendLine(
            $"                throw new NotSupportedException($\"Unknown component '{{componentName}}' on worker '{info.WorkerName}'.\");"
        );
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        return sb.ToString();
    }

    // ── Client-side code generation ─────────────────────────────────────

    private static string GenerateClientProxy(ComponentInterfaceInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Engine.Core;");
        sb.AppendLine("using NATS.Client.Core;");
        sb.AppendLine("using MessagePack;");
        sb.AppendLine();

        var proxyNamespace = !string.IsNullOrEmpty(info.InterfaceNamespace)
            ? info.InterfaceNamespace
            : "Engine.Client";

        sb.AppendLine($"namespace {proxyNamespace};");
        sb.AppendLine();
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Auto-generated NATS proxy for <see cref=\"{info.InterfaceName}\"/>.");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine(
            $"public sealed class {info.ProxyName} : {info.InterfaceFullName}, Engine.Client.IProxy"
        );
        sb.AppendLine("{");
        sb.AppendLine("    private readonly EntityId _entityId;");
        sb.AppendLine("    private readonly INatsConnection _nats;");
        sb.AppendLine();
        sb.AppendLine($"    public {info.ProxyName}(EntityId entityId, INatsConnection nats)");
        sb.AppendLine("    {");
        sb.AppendLine("        _entityId = entityId;");
        sb.AppendLine("        _nats = nats;");
        sb.AppendLine("    }");

        foreach (var method in info.Methods)
        {
            sb.AppendLine();
            var subject = $"component.{info.InterfaceName}.{method.Name}";

            if (method.ReturnDataType is not null)
            {
                // Task<T> method
                if (method.ParamType is not null)
                {
                    sb.AppendLine(
                        $"    public async Task<{method.ReturnDataType}> {method.Name}({method.ParamType} {method.ParamName}, CancellationToken ct = default)"
                    );
                    sb.AppendLine("    {");
                    sb.AppendLine(
                        $"        var requestPayload = MessagePackSerializer.Serialize(({method.ParamType})({method.ParamName}), cancellationToken: ct);"
                    );
                    sb.AppendLine(
                        $"        var headers = new NatsHeaders {{ {{ \"EntityId\", _entityId.Value.ToString() }} }};"
                    );
                    sb.AppendLine(
                        $"        var reply = await _nats.RequestAsync<byte[], byte[]>(\"{subject}\", requestPayload, headers: headers, cancellationToken: ct);"
                    );
                    sb.AppendLine(
                        $"        return MessagePackSerializer.Deserialize<{method.ReturnDataType}>(reply.Data, cancellationToken: ct);"
                    );
                    sb.AppendLine("    }");
                }
                else
                {
                    sb.AppendLine(
                        $"    public async Task<{method.ReturnDataType}> {method.Name}(CancellationToken ct = default)"
                    );
                    sb.AppendLine("    {");
                    sb.AppendLine(
                        $"        var reply = await _nats.RequestAsync<string, byte[]>(\"{subject}\", _entityId.Value.ToString(), cancellationToken: ct);"
                    );
                    sb.AppendLine(
                        $"        return MessagePackSerializer.Deserialize<{method.ReturnDataType}>(reply.Data, cancellationToken: ct);"
                    );
                    sb.AppendLine("    }");
                }
            }
            else
            {
                // Task method (no return value)
                if (method.ParamType is not null)
                {
                    sb.AppendLine(
                        $"    public async Task {method.Name}({method.ParamType} {method.ParamName}, CancellationToken ct = default)"
                    );
                    sb.AppendLine("    {");
                    sb.AppendLine(
                        $"        var requestPayload = MessagePackSerializer.Serialize(({method.ParamType})({method.ParamName}), cancellationToken: ct);"
                    );
                    sb.AppendLine(
                        $"        var headers = new NatsHeaders {{ {{ \"EntityId\", _entityId.Value.ToString() }} }};"
                    );
                    sb.AppendLine(
                        $"        var reply = await _nats.RequestAsync<byte[], string>(\"{subject}\", requestPayload, headers: headers, cancellationToken: ct);"
                    );
                    sb.AppendLine("        if (reply.Data is not \"ok\")");
                    sb.AppendLine(
                        $"            throw new InvalidOperationException($\"Component method '{method.Name}' failed: {{reply.Data}}\");"
                    );
                    sb.AppendLine("    }");
                }
                else
                {
                    sb.AppendLine(
                        $"    public async Task {method.Name}(CancellationToken ct = default)"
                    );
                    sb.AppendLine("    {");
                    sb.AppendLine(
                        $"        var reply = await _nats.RequestAsync<string, string>(\"{subject}\", _entityId.Value.ToString(), cancellationToken: ct);"
                    );
                    sb.AppendLine("        if (reply.Data is not \"ok\")");
                    sb.AppendLine(
                        $"            throw new InvalidOperationException($\"Component method '{method.Name}' failed: {{reply.Data}}\");"
                    );
                    sb.AppendLine("    }");
                }
            }
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    // ── Component proxy code generation ─────────────────────────────────

    private static string GenerateComponentProxy(ComponentStructInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Engine.Core;");
        sb.AppendLine("using NATS.Client.Core;");
        sb.AppendLine("using MessagePack;");
        sb.AppendLine();

        var proxyNamespace = !string.IsNullOrEmpty(info.StructNamespace)
            ? info.StructNamespace
            : "Engine.Client";

        sb.AppendLine($"namespace {proxyNamespace};");
        sb.AppendLine();
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Auto-generated component proxy for <see cref=\"{info.StructName}\"/>.");
        sb.AppendLine(
            $"/// Aggregates all behaviour interfaces declared via [Has&lt;&gt;] attributes."
        );
        sb.AppendLine($"/// </summary>");

        // Implement all behaviour interfaces + IProxy
        var interfaceList = string.Join(
            ", ",
            info.BehaviourInterfaces.Select(c => c.InterfaceFullName).Append("Engine.Client.IProxy")
        );
        sb.AppendLine($"public sealed class {info.ProxyName} : {interfaceList}");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly EntityId _entityId;");
        sb.AppendLine("    private readonly INatsConnection _nats;");
        sb.AppendLine();
        sb.AppendLine($"    public {info.ProxyName}(EntityId entityId, INatsConnection nats)");
        sb.AppendLine("    {");
        sb.AppendLine("        _entityId = entityId;");
        sb.AppendLine("        _nats = nats;");
        sb.AppendLine("    }");

        // Use explicit interface implementations to handle name collisions
        foreach (var component in info.BehaviourInterfaces)
        {
            foreach (var method in component.Methods)
            {
                sb.AppendLine();
                var subject = $"component.{component.InterfaceName}.{method.Name}";

                if (method.ReturnDataType is not null)
                {
                    // Task<T> method
                    if (method.ParamType is not null)
                    {
                        sb.AppendLine(
                            $"    async Task<{method.ReturnDataType}> {method.DeclaringInterfaceFullName}.{method.Name}({method.ParamType} {method.ParamName}, CancellationToken ct)"
                        );
                        sb.AppendLine("    {");
                        sb.AppendLine(
                            $"        var requestPayload = MessagePackSerializer.Serialize(({method.ParamType})({method.ParamName}), cancellationToken: ct);"
                        );
                        sb.AppendLine(
                            $"        var headers = new NatsHeaders {{ {{ \"EntityId\", _entityId.Value.ToString() }} }};"
                        );
                        sb.AppendLine(
                            $"        var reply = await _nats.RequestAsync<byte[], byte[]>(\"{subject}\", requestPayload, headers: headers, cancellationToken: ct);"
                        );
                        sb.AppendLine(
                            $"        return MessagePackSerializer.Deserialize<{method.ReturnDataType}>(reply.Data, cancellationToken: ct);"
                        );
                        sb.AppendLine("    }");
                    }
                    else
                    {
                        sb.AppendLine(
                            $"    async Task<{method.ReturnDataType}> {method.DeclaringInterfaceFullName}.{method.Name}(CancellationToken ct)"
                        );
                        sb.AppendLine("    {");
                        sb.AppendLine(
                            $"        var reply = await _nats.RequestAsync<string, byte[]>(\"{subject}\", _entityId.Value.ToString(), cancellationToken: ct);"
                        );
                        sb.AppendLine(
                            $"        return MessagePackSerializer.Deserialize<{method.ReturnDataType}>(reply.Data, cancellationToken: ct);"
                        );
                        sb.AppendLine("    }");
                    }
                }
                else
                {
                    // Task method (no return value)
                    if (method.ParamType is not null)
                    {
                        sb.AppendLine(
                            $"    async Task {method.DeclaringInterfaceFullName}.{method.Name}({method.ParamType} {method.ParamName}, CancellationToken ct)"
                        );
                        sb.AppendLine("    {");
                        sb.AppendLine(
                            $"        var requestPayload = MessagePackSerializer.Serialize(({method.ParamType})({method.ParamName}), cancellationToken: ct);"
                        );
                        sb.AppendLine(
                            $"        var headers = new NatsHeaders {{ {{ \"EntityId\", _entityId.Value.ToString() }} }};"
                        );
                        sb.AppendLine(
                            $"        var reply = await _nats.RequestAsync<byte[], string>(\"{subject}\", requestPayload, headers: headers, cancellationToken: ct);"
                        );
                        sb.AppendLine("        if (reply.Data is not \"ok\")");
                        sb.AppendLine(
                            $"            throw new InvalidOperationException($\"Component method '{method.Name}' failed: {{reply.Data}}\");"
                        );
                        sb.AppendLine("    }");
                    }
                    else
                    {
                        sb.AppendLine(
                            $"    async Task {method.DeclaringInterfaceFullName}.{method.Name}(CancellationToken ct)"
                        );
                        sb.AppendLine("    {");
                        sb.AppendLine(
                            $"        var reply = await _nats.RequestAsync<string, string>(\"{subject}\", _entityId.Value.ToString(), cancellationToken: ct);"
                        );
                        sb.AppendLine("        if (reply.Data is not \"ok\")");
                        sb.AppendLine(
                            $"            throw new InvalidOperationException($\"Component method '{method.Name}' failed: {{reply.Data}}\");"
                        );
                        sb.AppendLine("    }");
                    }
                }
            }
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    // ── Data models ─────────────────────────────────────────────────────

    private readonly struct WorkerInfo
    {
        public string WorkerFullName { get; }
        public string WorkerName { get; }
        public string WorkerNamespace { get; }
        public string MarkerStructName { get; }
        public string MarkerStructFullName { get; }
        public string MarkerStructNamespace { get; }
        public ImmutableArray<ComponentInfo> Components { get; }

        public WorkerInfo(
            string workerFullName,
            string workerName,
            string workerNamespace,
            string markerStructName,
            string markerStructFullName,
            string markerStructNamespace,
            ImmutableArray<ComponentInfo> components
        )
        {
            WorkerFullName = workerFullName;
            WorkerName = workerName;
            WorkerNamespace = workerNamespace;
            MarkerStructName = markerStructName;
            MarkerStructFullName = markerStructFullName;
            MarkerStructNamespace = markerStructNamespace;
            Components = components;
        }
    }

    private readonly struct ComponentInfo
    {
        public string ComponentName { get; }
        public string ComponentFullName { get; }
        public string ComponentNamespace { get; }
        public ImmutableArray<MethodInfo> Methods { get; }

        public ComponentInfo(
            string componentName,
            string componentFullName,
            string componentNamespace,
            ImmutableArray<MethodInfo> methods
        )
        {
            ComponentName = componentName;
            ComponentFullName = componentFullName;
            ComponentNamespace = componentNamespace;
            Methods = methods;
        }
    }

    private readonly struct ComponentInterfaceInfo
    {
        public string InterfaceName { get; }
        public string InterfaceFullName { get; }
        public string InterfaceNamespace { get; }
        public string ProxyName { get; }
        public ImmutableArray<MethodInfo> Methods { get; }

        public ComponentInterfaceInfo(
            string interfaceName,
            string interfaceFullName,
            string interfaceNamespace,
            string proxyName,
            ImmutableArray<MethodInfo> methods
        )
        {
            InterfaceName = interfaceName;
            InterfaceFullName = interfaceFullName;
            InterfaceNamespace = interfaceNamespace;
            ProxyName = proxyName;
            Methods = methods;
        }
    }

    private readonly struct MethodInfo
    {
        public string Name { get; }
        public string? ReturnDataType { get; }
        public string? ParamType { get; }
        public string ParamName { get; }

        /// <summary>
        /// The fully qualified name of the interface that declares this method.
        /// Used for explicit interface implementations in component proxies.
        /// </summary>
        public string DeclaringInterfaceFullName { get; }

        public MethodInfo(
            string name,
            string? returnDataType,
            string? paramType,
            string paramName,
            string declaringInterfaceFullName = ""
        )
        {
            Name = name;
            ReturnDataType = returnDataType;
            ParamType = paramType;
            ParamName = paramName;
            DeclaringInterfaceFullName = declaringInterfaceFullName;
        }
    }

    private readonly struct ComponentStructInfo
    {
        public string StructName { get; }
        public string StructFullName { get; }
        public string StructNamespace { get; }
        public string ProxyName { get; }
        public ImmutableArray<ComponentInterfaceInfo> BehaviourInterfaces { get; }

        public ComponentStructInfo(
            string structName,
            string structFullName,
            string structNamespace,
            string proxyName,
            ImmutableArray<ComponentInterfaceInfo> behaviourInterfaces
        )
        {
            StructName = structName;
            StructFullName = structFullName;
            StructNamespace = structNamespace;
            ProxyName = proxyName;
            BehaviourInterfaces = behaviourInterfaces;
        }
    }
}
