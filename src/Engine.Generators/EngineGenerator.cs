using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Engine.Generators;

/// <summary>
/// Roslyn incremental source generator that emits client proxies, server dispatch stubs,
/// and MessagePack resolver wiring for Engine types.
/// </summary>
[Generator]
public sealed class EngineGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Pipeline 1: IBehaviour-derived interfaces → client proxies
        var behaviourInterfaces = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is InterfaceDeclarationSyntax,
                transform: static (ctx, _) => GetBehaviourInterface(ctx)
            )
            .Where(static s => s is not null)
            .Select(static (s, _) => s!);

        context.RegisterSourceOutput(
            behaviourInterfaces.Collect(),
            static (spc, symbols) => ClientProxyEmitter.Emit(spc, symbols)
        );

        // Pipeline 2: Concrete classes implementing IBehaviour-derived interfaces → server stubs
        var behaviourImplementations = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => GetBehaviourImplementation(ctx)
            )
            .Where(static s => s is not null)
            .Select(static (s, _) => s!);

        context.RegisterSourceOutput(
            behaviourImplementations.Collect(),
            static (spc, symbols) => ServerStubEmitter.Emit(spc, symbols)
        );

        // Pipeline 3: IComponent implementations → MessagePack resolver
        var componentTypes = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node
                        is ClassDeclarationSyntax
                            or StructDeclarationSyntax
                            or RecordDeclarationSyntax,
                transform: static (ctx, _) => GetComponentType(ctx)
            )
            .Where(static s => s is not null)
            .Select(static (s, _) => s!);

        context.RegisterSourceOutput(
            componentTypes.Collect(),
            static (spc, symbols) => MessagePackEmitter.Emit(spc, symbols)
        );

        // Pipeline 4: Component<TContract> subclasses → partial class with events + interface
        var componentImpls = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => GetComponentImpl(ctx)
            )
            .Where(static s => s is not null)
            .Select(static (s, _) => s!);

        context.RegisterSourceOutput(
            componentImpls.Collect(),
            static (spc, symbols) => ComponentEmitter.Emit(spc, symbols)
        );
    }

    private static BehaviourInterfaceInfo? GetBehaviourInterface(GeneratorSyntaxContext ctx)
    {
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) as INamedTypeSymbol;
        if (symbol is null || !symbol.IsBehaviourInterface())
            return null;

        return BehaviourInterfaceInfo.From(symbol);
    }

    private static BehaviourImplInfo? GetBehaviourImplementation(GeneratorSyntaxContext ctx)
    {
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) as INamedTypeSymbol;
        if (symbol is null || !symbol.IsBehaviourImplementation())
            return null;

        return BehaviourImplInfo.From(symbol);
    }

    private static ComponentInfo? GetComponentType(GeneratorSyntaxContext ctx)
    {
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) as INamedTypeSymbol;
        if (symbol is null || !symbol.IsComponentImplementation())
            return null;

        return ComponentInfo.From(symbol);
    }

    private static ComponentImplInfo? GetComponentImpl(GeneratorSyntaxContext ctx)
    {
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) as INamedTypeSymbol;
        if (symbol is null || !symbol.IsComponentSubclass())
            return null;

        return ComponentImplInfo.From(symbol);
    }
}
