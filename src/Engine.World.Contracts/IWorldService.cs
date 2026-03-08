using Engine.Core;

namespace Engine.World.Contracts;

/// <summary>
/// Service contract for the distributed World.
/// Exposes entity and component management over NATS.
/// The source generator produces:
///   - WorldServiceClient (client proxy)
///   - WorldServiceServerBase (abstract server stub)
/// </summary>
[Generate]
public interface IWorldService
{
    /// <summary>
    /// Creates a new entity in the world and returns its identifier.
    /// </summary>
    Task<string> CreateEntity();

    /// <summary>
    /// Destroys an entity and all its components.
    /// </summary>
    Task DestroyEntity(string entityId);

    /// <summary>
    /// Registers a component type on the given entity.
    /// </summary>
    /// <param name="entityId">The entity to add the component to.</param>
    /// <param name="componentType">The assembly-qualified type name of the component.</param>
    Task<bool> AddComponent(string entityId, string componentType);

    /// <summary>
    /// Checks whether the given entity has a component assignable to the specified type.
    /// </summary>
    Task<bool> HasComponent(string entityId, string componentType);

    /// <summary>
    /// Removes a component of the specified type from the entity.
    /// </summary>
    Task<bool> RemoveComponent(string entityId, string componentType);
}
