using Engine.Core;
using NATS.Client.Core;

namespace Engine.Module;

/// <summary>
/// Client-side proxy to the backend WorldService.
/// All operations are forwarded over NATS request-reply.
/// </summary>
public sealed class World
{
    private readonly INatsConnection _nats;

    public World(INatsConnection nats)
    {
        _nats = nats;
    }

    /// <summary>
    /// Creates a new entity on the backend and returns a local <see cref="Entity"/> handle.
    /// </summary>
    public async Task<Entity> CreateEntityAsync(CancellationToken ct = default)
    {
        var reply = await _nats.RequestAsync<byte[], string>(
            "world.create",
            Array.Empty<byte>(),
            cancellationToken: ct
        );

        var id = ParseEntityId(reply.Data, "create");
        return new Entity(id);
    }

    /// <summary>
    /// Destroys an entity on the backend.
    /// </summary>
    public async Task DestroyEntityAsync(EntityId id, CancellationToken ct = default)
    {
        var reply = await _nats.RequestAsync<string, string>(
            "world.destroy",
            id.Value.ToString(),
            cancellationToken: ct
        );

        if (reply.Data is not "ok")
            throw new InvalidOperationException($"Failed to destroy entity {id}: {reply.Data}");
    }

    /// <summary>
    /// Returns whether the given entity exists on the backend.
    /// </summary>
    public async Task<bool> EntityExistsAsync(EntityId id, CancellationToken ct = default)
    {
        var reply = await _nats.RequestAsync<string, string>(
            "world.exists",
            id.Value.ToString(),
            cancellationToken: ct
        );

        return reply.Data is "true";
    }

    /// <summary>
    /// Lists all entity IDs known to the backend.
    /// </summary>
    public async Task<IReadOnlyList<EntityId>> ListEntitiesAsync(CancellationToken ct = default)
    {
        var reply = await _nats.RequestAsync<byte[], string>(
            "world.list",
            Array.Empty<byte>(),
            cancellationToken: ct
        );

        if (string.IsNullOrEmpty(reply.Data))
            return Array.Empty<EntityId>();

        return reply.Data!.Split(',').Select(s => new EntityId(Guid.Parse(s))).ToArray();
    }

    private static EntityId ParseEntityId(string? data, string operation)
    {
        if (data is null || !Guid.TryParse(data, out var guid))
            throw new InvalidOperationException(
                $"Backend returned invalid EntityId from {operation}: {data}"
            );

        return new EntityId(guid);
    }
}
