using Engine.Core;

namespace Engine.Hierarchy;

/// <summary>
/// Component contract for attaching a parent relationship to an entity.
/// </summary>
public interface IParent : IComponentData<EntityId> { }
