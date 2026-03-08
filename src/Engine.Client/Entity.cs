using Engine.Core;
using NATS.Client.Core;

namespace Engine.Client;

/// <summary>
/// Client-side entity proxy that communicates with the backend over NATS.
/// </summary>
public sealed class Entity : IEntity
{
    private readonly INatsConnection _connection;

    public Entity(EntityId id, INatsConnection connection)
    {
        Id = id;
        _connection = connection;
    }

    public EntityId Id { get; }

    public async Task AddComponentAsync<T>(CancellationToken ct)
        where T : IComponent
    {
        await _connection.PublishAsync(
            $"entity.{Id}.component.add.{typeof(T).Name}",
            cancellationToken: ct
        );
    }

    public async Task RemoveComponentAsync<T>(CancellationToken ct)
        where T : IComponent
    {
        await _connection.PublishAsync(
            $"entity.{Id}.component.remove.{typeof(T).Name}",
            cancellationToken: ct
        );
    }

    public Task<T> GetComponentAsync<T>(CancellationToken ct)
        where T : IComponent
    {
        // TODO: Implement request/reply with MessagePack deserialization
        throw new NotImplementedException();
    }

    public Task<bool> HasComponentAsync<T>(CancellationToken ct)
        where T : IComponent
    {
        // TODO: Implement request/reply
        throw new NotImplementedException();
    }
}
