using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Engine.Generators;

[Generator]
public sealed class WorkerGenerator : IIncrementalGenerator
{
    private const string ComponentWorkerFqn = "Engine.Worker.ComponentWorker";
    private const string HasBehaviourAttributeFqn = "Engine.Client.HasBehaviourAttribute";
    private const string IBehaviourFqn = "Engine.Client.IBehaviour";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all partial classes that might inherit ComponentWorker<T>.
        var workerDeclarations = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax cds
                    && cds.Modifiers.Any(SyntaxKind.PartialKeyword),
                transform: static (ctx, ct) =>
                    ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) as INamedTypeSymbol
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
                            new ComponentInfo(iface.Name, iface.ToDisplayString(), methods)
                        );
                    }

                    if (allComponents.Count == 0)
                        return default;

                    return new WorkerInfo(
                        symbol!.Name,
                        symbol.ContainingNamespace.IsGlobalNamespace
                            ? ""
                            : symbol.ContainingNamespace.ToDisplayString(),
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

    private static ImmutableArray<INamedTypeSymbol> GetComponentInterfaces(
        INamedTypeSymbol markerStruct,
        Compilation compilation
    )
    {
        var hasAttributeType = compilation.GetTypeByMetadataName($"{HasBehaviourAttributeFqn}`1");
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
        if (iBehaviour is not null && SymbolEqualityComparer.Default.Equals(iface, iBehaviour))
            return;

        foreach (var member in iface.GetMembers())
        {
            if (member is not IMethodSymbol method)
                continue;
            if (method.MethodKind != MethodKind.Ordinary)
                continue;
            if (!seen.Add(method.Name))
                continue;

            var isTask = IsTask(method.ReturnType, out var returnDataType);
            if (!isTask)
                continue;

            string? paramType = null;
            string? paramName = null;
            foreach (var p in method.Parameters)
            {
                if (p.Type.ToDisplayString() == "System.Threading.CancellationToken")
                    continue;
                if (paramType is not null)
                    goto NextMethod;
                paramType = p.Type.ToDisplayString();
                paramName = p.Name;
            }

            builder.Add(
                new MethodInfo(method.Name, returnDataType, paramType, paramName ?? "data")
            );

            NextMethod:
            ;
        }

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

    // ── Code generation ─────────────────────────────────────────────────

    private static string GenerateWorkerPartial(WorkerInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Engine.Worker;");
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

    // ── Data models ─────────────────────────────────────────────────────

    private readonly struct WorkerInfo
    {
        public string WorkerName { get; }
        public string WorkerNamespace { get; }
        public ImmutableArray<ComponentInfo> Components { get; }

        public WorkerInfo(
            string workerName,
            string workerNamespace,
            ImmutableArray<ComponentInfo> components
        )
        {
            WorkerName = workerName;
            WorkerNamespace = workerNamespace;
            Components = components;
        }
    }

    private readonly struct ComponentInfo
    {
        public string ComponentName { get; }
        public string ComponentFullName { get; }
        public ImmutableArray<MethodInfo> Methods { get; }

        public ComponentInfo(
            string componentName,
            string componentFullName,
            ImmutableArray<MethodInfo> methods
        )
        {
            ComponentName = componentName;
            ComponentFullName = componentFullName;
            Methods = methods;
        }
    }

    private readonly struct MethodInfo
    {
        public string Name { get; }
        public string? ReturnDataType { get; }
        public string? ParamType { get; }
        public string ParamName { get; }

        public MethodInfo(string name, string? returnDataType, string? paramType, string paramName)
        {
            Name = name;
            ReturnDataType = returnDataType;
            ParamType = paramType;
            ParamName = paramName;
        }
    }
}
