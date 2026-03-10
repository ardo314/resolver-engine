namespace Engine.Core;

/// <summary>
/// Non-generic base class for all per-entity component instances.
/// Provides the owning <see cref="Entity"/> reference, which is set by the
/// framework before <see cref="IComponent.OnAddAsync"/> is called.
/// </summary>
public abstract class Component
{
    /// <summary>
    /// The entity this component instance belongs to.
    /// Set by the framework during <see cref="Entity.AddComponentAsync"/>,
    /// before <see cref="IComponent.OnAddAsync"/> is called.
    /// </summary>
    public Entity Entity { get; internal set; } = null!;
}

/// <summary>
/// Abstract base class for per-entity component implementations.
/// Derive from this class using the component contract interface as the type parameter
/// (e.g., <c>class MyPose : Component&lt;IPose&gt;</c>).
/// <para>
/// Each entity that has this component gets its own instance.
/// The owning <see cref="Component.Entity"/> is available in all lifecycle methods.
/// </para>
/// <para>
/// The source generator detects subclasses, resolves the data type from the contract,
/// and emits a partial class that:
/// <list type="bullet">
///   <item>Adds the contract interface (e.g., <c>: IPose</c>)</item>
///   <item>Implements <see cref="IComponent{TData}.Updated"/> event</item>
///   <item>Provides a <c>RaiseUpdated</c> helper method</item>
/// </list>
/// </para>
/// <para>
/// Mark your subclass <c>partial</c> so the generator can extend it.
/// Call <c>RaiseUpdated(data)</c> in your <c>SetAsync</c> implementation.
/// </para>
/// </summary>
/// <typeparam name="TContract">
/// The component contract interface that extends <see cref="IComponent{TData}"/>.
/// </typeparam>
public abstract class Component<TContract> : Component
    where TContract : IComponent { }
