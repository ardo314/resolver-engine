namespace Engine.Core;

/// <summary>
/// Marker interface for all components.
/// A component holds data or state that can be attached to an Entity.
/// Each component instance is bound to exactly one entity.
/// </summary>
public interface IComponent
{
    /// <summary>
    /// Called when the component is removed from its entity.
    /// Implementations should clean up any internal state.
    /// </summary>
    Task OnRemoveAsync(CancellationToken ct = default);
}

/// <summary>
/// A typed component that stores data of type <typeparamref name="TData"/>.
/// Each instance belongs to exactly one entity. Implementations must fire
/// <see cref="Updated"/> from their <see cref="SetAsync"/> method when the
/// operation succeeds.
/// </summary>
/// <typeparam name="TData">The data type this component stores.</typeparam>
public interface IComponent<TData> : IComponent
{
    /// <summary>
    /// Called when the component is added to an entity.
    /// The owning entity is available via <see cref="Component.Entity"/>.
    /// </summary>
    Task OnAddAsync(TData initialData, CancellationToken ct = default);

    /// <summary>
    /// Gets the component data.
    /// </summary>
    Task<TData> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets the component data.
    /// Implementations should call <c>RaiseUpdated</c> after a successful write.
    /// </summary>
    Task SetAsync(TData data, CancellationToken ct = default);

    /// <summary>
    /// Fired by the implementation when component data is successfully updated.
    /// </summary>
    event Action<TData>? Updated;
}
