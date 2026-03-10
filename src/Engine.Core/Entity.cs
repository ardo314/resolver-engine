namespace Engine.Core;

/// <summary>
/// A uniquely identified object in the world. Entities are containers for components.
/// Each component instance is per-entity — adding a component creates a new instance
/// bound to this entity. The component registry enforces that only one implementation
/// of a given component contract interface (e.g., <c>IPose</c>) can be attached at a time.
/// </summary>
public sealed class Entity
{
    // Contract type (e.g., IPose) → component instance
    private readonly Dictionary<Type, IComponent> _componentsByContract = new();

    // Concrete type (e.g., InMemoryPose) → contract type (e.g., IPose)
    private readonly Dictionary<Type, Type> _implToContract = new();

    /// <summary>
    /// The unique identifier of this entity.
    /// </summary>
    public EntityId Id { get; }

    /// <summary>
    /// Fired when a component is added to this entity.
    /// </summary>
    public event Action<IComponent>? ComponentAdded;

    /// <summary>
    /// Fired when a component is removed from this entity.
    /// </summary>
    public event Action<IComponent>? ComponentRemoved;

    /// <summary>
    /// Creates a new entity with the given identifier.
    /// </summary>
    internal Entity(EntityId id) => Id = id;

    /// <summary>
    /// Adds a component of concrete type <typeparamref name="TImpl"/> to this entity.
    /// A new instance is created and bound to this entity. Returns a typed
    /// <see cref="ComponentHandle{TData}"/> for convenient access.
    /// Only one implementation per contract interface is allowed; adding a second
    /// implementation for the same contract throws <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <typeparam name="TImpl">The concrete component type (e.g., <c>InMemoryPose</c>).</typeparam>
    /// <typeparam name="TData">The data type stored by the component (e.g., <c>Pose</c>).</typeparam>
    /// <param name="initialData">Optional initial data passed to <see cref="IComponent{TData}.OnAddAsync"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<ComponentHandle<TData>> AddComponentAsync<TImpl, TData>(
        TData initialData = default!,
        CancellationToken ct = default
    )
        where TImpl : class, IComponent<TData>, new()
    {
        var implType = typeof(TImpl);
        var contractType = ResolveContractType(implType);

        if (_componentsByContract.ContainsKey(contractType))
        {
            throw new InvalidOperationException(
                $"Entity {Id} already has a component for contract {contractType.Name}. "
                    + $"Remove the existing component before adding a new implementation."
            );
        }

        var component = new TImpl();

        // Inject the owning entity before calling OnAddAsync
        if (component is Component baseComponent)
        {
            baseComponent.Entity = this;
        }

        await component.OnAddAsync(initialData, ct);

        _componentsByContract[contractType] = component;
        _implToContract[implType] = contractType;

        ComponentAdded?.Invoke(component);

        return new ComponentHandle<TData>(this, component);
    }

    /// <summary>
    /// Gets a typed component handle by contract or concrete type.
    /// Returns <c>null</c> if no matching component is registered.
    /// </summary>
    /// <typeparam name="T">The contract interface (e.g., <c>IPose</c>) or concrete type (e.g., <c>InMemoryPose</c>).</typeparam>
    /// <typeparam name="TData">The data type stored by the component.</typeparam>
    public Task<ComponentHandle<TData>?> GetComponentAsync<T, TData>(CancellationToken ct = default)
        where T : IComponent<TData>
    {
        var component = ResolveComponent(typeof(T));
        if (component is IComponent<TData> typed)
            return Task.FromResult<ComponentHandle<TData>?>(
                new ComponentHandle<TData>(this, typed)
            );

        return Task.FromResult<ComponentHandle<TData>?>(null);
    }

    /// <summary>
    /// Gets a non-generic component handle by contract or concrete type.
    /// Returns <c>null</c> if no matching component is registered.
    /// </summary>
    public Task<IComponentHandle?> GetComponentAsync<T>(CancellationToken ct = default)
        where T : IComponent
    {
        var component = ResolveComponent(typeof(T));
        if (component is null)
            return Task.FromResult<IComponentHandle?>(null);

        // Build a ComponentHandle<TData> via reflection to find TData
        var dataType = GetDataType(component.GetType());
        if (dataType is null)
            return Task.FromResult<IComponentHandle?>(null);

        var handleType = typeof(ComponentHandle<>).MakeGenericType(dataType);
        var handle = Activator.CreateInstance(
            handleType,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null,
            [this, component],
            null
        );
        return Task.FromResult((IComponentHandle?)handle);
    }

    /// <summary>
    /// Returns <c>true</c> if the entity has a component matching the given contract or concrete type.
    /// </summary>
    public Task<bool> HasComponentAsync<T>(CancellationToken ct = default)
        where T : IComponent
    {
        var found = ResolveComponent(typeof(T)) is not null;
        return Task.FromResult(found);
    }

    /// <summary>
    /// Removes the component matching the given contract or concrete type.
    /// If no matching component exists this is a no-op.
    /// </summary>
    public async Task RemoveComponentAsync<T>(CancellationToken ct = default)
        where T : IComponent
    {
        var type = typeof(T);
        Type? contractType = null;

        if (_componentsByContract.ContainsKey(type))
        {
            contractType = type;
        }
        else if (_implToContract.TryGetValue(type, out var mapped))
        {
            contractType = mapped;
        }

        if (contractType is null)
            return;

        if (!_componentsByContract.TryGetValue(contractType, out var component))
            return;

        await component.OnRemoveAsync(ct);

        _componentsByContract.Remove(contractType);

        // Remove all impl→contract mappings pointing to this contract
        var implKeys = _implToContract
            .Where(kv => kv.Value == contractType)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in implKeys)
            _implToContract.Remove(key);

        ComponentRemoved?.Invoke(component);
    }

    /// <summary>
    /// Resolves a behaviour of the given contract type.
    /// </summary>
    /// <remarks>
    /// Behaviour resolution is handled by the runtime (e.g., via NATS proxies).
    /// This method currently throws; it will be wired up when the runtime is connected.
    /// </remarks>
    public Task<T> GetBehaviourAsync<T>(CancellationToken ct = default)
        where T : IBehaviour
    {
        // TODO: Wire to runtime behaviour resolution (e.g., NATS proxy lookup)
        throw new NotImplementedException(
            $"Behaviour resolution for {typeof(T).Name} is not yet implemented."
        );
    }

    /// <summary>
    /// Removes all components from this entity (called during entity destruction).
    /// </summary>
    internal async Task RemoveAllComponentsAsync(CancellationToken ct = default)
    {
        foreach (var component in _componentsByContract.Values.ToList())
        {
            await component.OnRemoveAsync(ct);
            ComponentRemoved?.Invoke(component);
        }

        _componentsByContract.Clear();
        _implToContract.Clear();
    }

    /// <summary>
    /// Resolves a component by contract type or concrete impl type.
    /// </summary>
    private IComponent? ResolveComponent(Type type)
    {
        if (_componentsByContract.TryGetValue(type, out var byContract))
            return byContract;

        if (_implToContract.TryGetValue(type, out var contractType))
        {
            if (_componentsByContract.TryGetValue(contractType, out var byImpl))
                return byImpl;
        }

        return null;
    }

    /// <summary>
    /// Finds the contract interface type for a concrete component type.
    /// The contract is the first interface that directly extends <see cref="IComponent{TData}"/>
    /// (excluding <see cref="IComponent{TData}"/> itself and <see cref="IComponent"/>).
    /// </summary>
    private static Type ResolveContractType(Type implType)
    {
        // Walk the base class chain to find Component<TContract>
        var baseType = implType.BaseType;
        while (baseType is not null)
        {
            if (
                baseType.IsGenericType
                && baseType.GetGenericTypeDefinition() == typeof(Component<>)
            )
            {
                return baseType.GetGenericArguments()[0];
            }
            baseType = baseType.BaseType;
        }

        // Fallback: first interface implementing IComponent<TData>
        foreach (var iface in implType.GetInterfaces())
        {
            if (
                iface != typeof(IComponent)
                && typeof(IComponent).IsAssignableFrom(iface)
                && iface
                    .GetInterfaces()
                    .Any(i =>
                        i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IComponent<>)
                    )
            )
            {
                return iface;
            }
        }

        // Last resort: use the concrete type itself
        return implType;
    }

    /// <summary>
    /// Extracts the TData type from a component type implementing IComponent&lt;TData&gt;.
    /// </summary>
    private static Type? GetDataType(Type componentType)
    {
        foreach (var iface in componentType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IComponent<>))
                return iface.GetGenericArguments()[0];
        }
        return null;
    }

    public override string ToString() => Id.ToString();
}
