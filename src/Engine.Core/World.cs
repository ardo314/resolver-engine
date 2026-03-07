using System;
using System.Collections.Generic;

namespace Engine.Core;

/// <summary>
/// The top-level container that owns entities and their component storage.
/// </summary>
public class World
{
    private readonly Dictionary<Entity, List<IComponent>> _components = new();

    /// <summary>
    /// Creates a new entity in this world.
    /// </summary>
    public EntityRef CreateEntity()
    {
        var entity = Entity.New();
        _components[entity] = new List<IComponent>();
        return new EntityRef(entity, this);
    }

    /// <summary>
    /// Destroys an entity and all its components.
    /// </summary>
    public void DestroyEntity(Entity entity)
    {
        _components.Remove(entity);
    }

    /// <summary>
    /// Registers a component instance on the given entity.
    /// </summary>
    internal void AddComponent(Entity entity, IComponent component)
    {
        if (!_components.TryGetValue(entity, out var list))
            throw new InvalidOperationException($"Entity {entity} does not exist in this world.");

        list.Add(component);
    }

    /// <summary>
    /// Finds the first component assignable to <typeparamref name="T"/> on the given entity.
    /// </summary>
    internal T? FindComponent<T>(Entity entity)
        where T : class, IComponent
    {
        if (!_components.TryGetValue(entity, out var list))
            return null;

        foreach (var component in list)
        {
            if (component is T typed)
                return typed;
        }

        return null;
    }

    /// <summary>
    /// Removes the first component assignable to <typeparamref name="T"/> from the given entity.
    /// </summary>
    internal bool RemoveComponent<T>(Entity entity)
        where T : class, IComponent
    {
        if (!_components.TryGetValue(entity, out var list))
            return false;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] is T)
            {
                list.RemoveAt(i);
                return true;
            }
        }

        return false;
    }
}
