using Engine.Core;
using NATS.Client.Core;

namespace Engine.Client;

/// <summary>
/// Client-side world proxy that communicates with the backend over NATS.
/// </summary>
public sealed class World : IWorld
{
    private readonly INatsConnection _connection;

    public World(INatsConnection connection)
    {
        _connection = connection;
    }

    public Task<IEntity> CreateEntityAsync(CancellationToken ct = default)
    {
        // TODO: Implement request/reply to backend for entity creation
        throw new NotImplementedException();
    }

    public async Task DestroyEntityAsync(EntityId id, CancellationToken ct = default)
    {
        await _connection.PublishAsync($"entity.{id}.destroy", cancellationToken: ct);
    }

    public Task<IEntity> GetEntityAsync(EntityId id, CancellationToken ct = default)
    {
        IEntity entity = new Entity(id, _connection);
        return Task.FromResult(entity);
    }
}
