using System;

namespace Engine.Core;

/// <summary>
/// A convenience handle that binds an <see cref="Entity"/> to its <see cref="World"/>,
/// allowing direct component operations.
/// </summary>
public readonly struct EntityRef : IEquatable<EntityRef>
{
    /// <summary>
    /// The underlying entity identifier.
    /// </summary>
    public Entity Entity { get; }

    private readonly World _world;

    internal EntityRef(Entity entity, World world)
    {
        Entity = entity;
        _world = world;
    }

    /// <summary>
    /// Creates and adds a new component of type <typeparamref name="T"/> to this entity.
    /// </summary>
    public T AddComponent<T>()
        where T : IComponent, new()
    {
        var component = new T();
        _world.AddComponent(Entity, component);
        return component;
    }

    /// <summary>
    /// Gets the component of type <typeparamref name="T"/> from this entity.
    /// </summary>
    /// <exception cref="ComponentNotFoundException">No matching component found.</exception>
    public T GetComponent<T>()
        where T : class, IComponent
    {
        return _world.FindComponent<T>(Entity)
            ?? throw new ComponentNotFoundException(typeof(T), Entity);
    }

    /// <summary>
    /// Returns <c>true</c> if this entity has a component assignable to <typeparamref name="T"/>.
    /// </summary>
    public bool HasComponent<T>()
        where T : class, IComponent
    {
        return _world.FindComponent<T>(Entity) is not null;
    }

    /// <summary>
    /// Removes the first component assignable to <typeparamref name="T"/> from this entity.
    /// </summary>
    public bool RemoveComponent<T>()
        where T : class, IComponent
    {
        return _world.RemoveComponent<T>(Entity);
    }

    /// <summary>
    /// Implicitly converts an <see cref="EntityRef"/> to its underlying <see cref="Entity"/>.
    /// </summary>
    public static implicit operator Entity(EntityRef entityRef) => entityRef.Entity;

    public bool Equals(EntityRef other) => Entity.Equals(other.Entity);

    public override bool Equals(object? obj) => obj is EntityRef other && Equals(other);

    public override int GetHashCode() => Entity.GetHashCode();

    public override string ToString() => Entity.ToString();

    public static bool operator ==(EntityRef left, EntityRef right) => left.Equals(right);

    public static bool operator !=(EntityRef left, EntityRef right) => !left.Equals(right);
}
