using Engine.Core;

namespace Engine.Backend;

/// <summary>
/// Server-side world implementation that manages entity lifecycle.
/// </summary>
public sealed class World : IWorld
{
    private readonly EntityStore _store = new();

    public Task<IEntity> CreateEntityAsync(CancellationToken ct = default)
    {
        var record = _store.Create();
        IEntity entity = new Entity(record);
        return Task.FromResult(entity);
    }

    public Task DestroyEntityAsync(EntityId id, CancellationToken ct = default)
    {
        if (!_store.Remove(id))
        {
            throw new KeyNotFoundException($"Entity {id} not found.");
        }

        return Task.CompletedTask;
    }

    public Task<IEntity> GetEntityAsync(EntityId id, CancellationToken ct = default)
    {
        var record = _store.Get(id);
        IEntity entity = new Entity(record);
        return Task.FromResult(entity);
    }
}
