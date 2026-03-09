using Engine.Core;

namespace Engine.Hierarchy;

/// <summary>
/// Data for the Parent component — stores a reference to the parent entity.
/// </summary>
public struct Parent
{
    /// <summary>
    /// The <see cref="EntityId"/> of the parent entity.
    /// </summary>
    public EntityId ParentId { get; init; }
}

/// <summary>
/// Component contract for attaching a parent relationship to an entity.
/// </summary>
public interface IParent : IComponent<Parent> { }
