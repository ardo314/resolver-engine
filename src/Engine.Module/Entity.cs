namespace Engine.Module;

using Engine.Core;

public class Entity
{
    public EntityId Id { get; }

    public Entity(EntityId id)
    {
        Id = id;
    }

    public async Task AddComponentAsync<TComponent>(
        TComponent component,
        CancellationToken ct = default
    )
        where TComponent : Component
    {
        // Implementation to add the component to the entity
    }

    public async Task RemoveComponentAsync<TComponent>(CancellationToken ct = default)
        where TComponent : Component
    {
        // Implementation to remove the component from the entity
    }

    public async Task<bool> HasComponentAsync<TComponent>(CancellationToken ct = default)
        where TComponent : Component
    {
        // Implementation to check if the entity has the component
        return false;
    }

    public async Task<TComponent> GetComponentAsync<TComponent>(CancellationToken ct = default)
        where TComponent : Component
    {
        // Implementation to retrieve the component from the entity
        return default!;
    }
}
