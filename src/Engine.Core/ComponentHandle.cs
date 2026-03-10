namespace Engine.Core;

/// <summary>
/// Non-generic base interface for component handles.
/// Returned by single-type-parameter overloads on <see cref="Entity"/>.
/// </summary>
public interface IComponentHandle
{
    /// <summary>
    /// Fired when the component is removed from the entity.
    /// </summary>
    event Action? Removed;
}

/// <summary>
/// A typed, entity-bound view of a component.
/// Wraps an <see cref="IComponent{TData}"/> instance and its owning <see cref="Entity"/>
/// so callers can use <see cref="GetAsync"/> and <see cref="SetAsync"/> without passing
/// the entity on every call.
/// </summary>
/// <typeparam name="TData">The component data type.</typeparam>
public sealed class ComponentHandle<TData> : IComponentHandle
{
    private readonly Entity _entity;
    private readonly IComponent<TData> _component;

    /// <summary>
    /// Fired when the component data is successfully updated.
    /// </summary>
    public event Action<TData>? Updated;

    /// <summary>
    /// Fired when the component is removed from this entity.
    /// </summary>
    public event Action? Removed;

    internal ComponentHandle(Entity entity, IComponent<TData> component)
    {
        _entity = entity;
        _component = component;

        // Component is per-entity, so no filtering needed.
        _component.Updated += OnDataUpdated;
        _entity.ComponentRemoved += OnComponentRemoved;
    }

    /// <summary>
    /// Gets the component data.
    /// </summary>
    public Task<TData> GetAsync(CancellationToken ct = default) => _component.GetAsync(ct);

    /// <summary>
    /// Sets the component data.
    /// </summary>
    public Task SetAsync(TData data, CancellationToken ct = default) =>
        _component.SetAsync(data, ct);

    private void OnDataUpdated(TData data)
    {
        Updated?.Invoke(data);
    }

    private void OnComponentRemoved(IComponent component)
    {
        if (ReferenceEquals(component, _component))
        {
            Removed?.Invoke();
            Detach();
        }
    }

    /// <summary>
    /// Detaches this handle from events.
    /// Called automatically when the component is removed.
    /// </summary>
    internal void Detach()
    {
        _component.Updated -= OnDataUpdated;
        _entity.ComponentRemoved -= OnComponentRemoved;
    }
}
