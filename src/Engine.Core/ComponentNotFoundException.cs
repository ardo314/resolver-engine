using System;

namespace Engine.Core;

/// <summary>
/// Thrown when a requested component type is not found on an entity.
/// </summary>
public class ComponentNotFoundException : InvalidOperationException
{
    public Type ComponentType { get; }
    public Entity Entity { get; }

    public ComponentNotFoundException(Type componentType, Entity entity)
        : base($"Component of type '{componentType.Name}' not found on entity {entity}.")
    {
        ComponentType = componentType;
        Entity = entity;
    }
}
