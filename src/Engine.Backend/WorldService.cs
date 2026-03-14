using System.Collections.Concurrent;
using Engine.Core;
using NATS.Client.Core;
using NATS.Client.Services;

namespace Engine.Backend;

/// <summary>
/// Manages entity lifecycles (create / destroy) and exposes them as a NATS service.
///
/// NATS subjects (under the "world" service group):
///   world.create  – request with empty payload, replies with the new EntityId (Guid string).
///   world.destroy  – request with EntityId (Guid string), replies with "ok" or an error.
///   world.exists   – request with EntityId (Guid string), replies with "true" / "false".
///   world.list     – request with empty payload, replies with comma-separated EntityId list.
/// </summary>
public sealed class WorldService : IAsyncDisposable
{
    private readonly ConcurrentDictionary<EntityId, byte> _entities = new();
    private readonly NatsSvcServer _svc;

    public WorldService(INatsConnection nats, CancellationToken ct)
    {
        _svc = new NatsSvcServer(nats, new NatsSvcConfig("world", "1.0.0"), ct);
    }

    /// <summary>
    /// Registers all NATS service endpoints and begins listening for requests.
    /// </summary>
    public async Task StartAsync()
    {
        var grp = await _svc.AddGroupAsync("world");

        await grp.AddEndpointAsync<byte[]>(name: "create", handler: HandleCreateAsync);
        await grp.AddEndpointAsync<string>(name: "destroy", handler: HandleDestroyAsync);
        await grp.AddEndpointAsync<string>(name: "exists", handler: HandleExistsAsync);
        await grp.AddEndpointAsync<byte[]>(name: "list", handler: HandleListAsync);
    }

    private async ValueTask HandleCreateAsync(NatsSvcMsg<byte[]> msg)
    {
        var id = EntityId.New();
        _entities.TryAdd(id, 0);
        await msg.ReplyAsync(id.Value.ToString());
    }

    private async ValueTask HandleDestroyAsync(NatsSvcMsg<string> msg)
    {
        if (!Guid.TryParse(msg.Data, out var guid))
        {
            await msg.ReplyErrorAsync(400, "Invalid EntityId format");
            return;
        }

        var id = new EntityId(guid);
        if (_entities.TryRemove(id, out _))
        {
            await msg.ReplyAsync("ok");
        }
        else
        {
            await msg.ReplyErrorAsync(404, "Entity not found");
        }
    }

    private async ValueTask HandleExistsAsync(NatsSvcMsg<string> msg)
    {
        if (!Guid.TryParse(msg.Data, out var guid))
        {
            await msg.ReplyErrorAsync(400, "Invalid EntityId format");
            return;
        }

        var id = new EntityId(guid);
        var exists = _entities.ContainsKey(id);
        await msg.ReplyAsync(exists ? "true" : "false");
    }

    private async ValueTask HandleListAsync(NatsSvcMsg<byte[]> msg)
    {
        var ids = string.Join(",", _entities.Keys.Select(e => e.Value.ToString()));
        await msg.ReplyAsync(ids);
    }

    public async ValueTask DisposeAsync()
    {
        await _svc.DisposeAsync();
    }
}
