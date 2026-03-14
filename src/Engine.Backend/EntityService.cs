using Engine.Core;
using NATS.Client.Core;
using NATS.Client.Services;

namespace Engine.Backend;

/// <summary>
/// Manages entity lifecycles and behaviour tracking, exposed as a NATS service.
///
/// When adding or removing behaviours, the backend first sends a NATS request to the
/// module runtime (worker.create / worker.remove) and only commits to the entity
/// registry if the runtime responds successfully.
///
/// NATS subjects (under the "entity" service group):
///   entity.create           – request with empty payload, replies with the new EntityId (Guid string).
///   entity.destroy          – request with EntityId (Guid string), replies with "ok" or an error.
///   entity.exists           – request with EntityId (Guid string), replies with "true" / "false".
///   entity.list             – request with empty payload, replies with comma-separated EntityId list.
///   entity.add-behaviour    – request "entityId:behaviourName", replies "ok" or error.
///   entity.remove-behaviour – request "entityId:behaviourName", replies "ok" or error.
///   entity.has-behaviour    – request "entityId:behaviourName", replies "true" / "false".
///   entity.list-behaviours  – request EntityId (Guid string), replies comma-separated names.
/// </summary>
public sealed class EntityService : IAsyncDisposable
{
    private readonly INatsConnection _nats;
    private readonly EntityRepository _repo;
    private readonly NatsSvcServer _svc;

    public EntityService(INatsConnection nats, EntityRepository repo, CancellationToken ct)
    {
        _nats = nats;
        _repo = repo;
        _svc = new NatsSvcServer(nats, new NatsSvcConfig("entity", "1.0.0"), ct);
    }

    /// <summary>
    /// Registers all NATS service endpoints and begins listening for requests.
    /// </summary>
    public async Task StartAsync()
    {
        var grp = await _svc.AddGroupAsync("entity");

        // Entity lifecycle
        await grp.AddEndpointAsync<byte[]>(name: "create", handler: HandleCreateAsync);
        await grp.AddEndpointAsync<string>(name: "destroy", handler: HandleDestroyAsync);
        await grp.AddEndpointAsync<string>(name: "exists", handler: HandleExistsAsync);
        await grp.AddEndpointAsync<byte[]>(name: "list", handler: HandleListAsync);

        // Behaviour management
        await grp.AddEndpointAsync<string>(name: "add-behaviour", handler: HandleAddBehaviourAsync);
        await grp.AddEndpointAsync<string>(
            name: "remove-behaviour",
            handler: HandleRemoveBehaviourAsync
        );
        await grp.AddEndpointAsync<string>(name: "has-behaviour", handler: HandleHasBehaviourAsync);
        await grp.AddEndpointAsync<string>(
            name: "list-behaviours",
            handler: HandleListBehavioursAsync
        );
    }

    // ── Entity lifecycle ────────────────────────────────────────────────

    private async ValueTask HandleCreateAsync(NatsSvcMsg<byte[]> msg)
    {
        var id = _repo.Create();
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
        if (_repo.Destroy(id))
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
        var exists = _repo.Exists(id);
        await msg.ReplyAsync(exists ? "true" : "false");
    }

    private async ValueTask HandleListAsync(NatsSvcMsg<byte[]> msg)
    {
        var ids = string.Join(",", _repo.ListAll().Select(e => e.Value.ToString()));
        await msg.ReplyAsync(ids);
    }

    // ── Behaviour management ────────────────────────────────────────────

    private async ValueTask HandleAddBehaviourAsync(NatsSvcMsg<string> msg)
    {
        if (!TryParseRequest(msg.Data, out var entityId, out var behaviourName))
        {
            await msg.ReplyErrorAsync(400, "Expected format: entityId:behaviourName");
            return;
        }

        if (!_repo.Exists(entityId))
        {
            await msg.ReplyErrorAsync(404, "Entity not found");
            return;
        }

        if (_repo.HasBehaviour(entityId, behaviourName))
        {
            await msg.ReplyErrorAsync(409, "Behaviour already added");
            return;
        }

        // Request the module runtime to create a worker for this behaviour.
        try
        {
            var workerReply = await _nats.RequestAsync<string, string>(
                $"worker.create.{behaviourName}",
                entityId.Value.ToString()
            );

            if (workerReply.Data is not "ok")
            {
                await msg.ReplyErrorAsync(502, $"Module runtime error: {workerReply.Data}");
                return;
            }
        }
        catch (NatsNoRespondersException)
        {
            await msg.ReplyErrorAsync(502, "No module handles this behaviour");
            return;
        }

        if (!_repo.AddBehaviour(entityId, behaviourName))
        {
            await msg.ReplyErrorAsync(409, "Behaviour already added");
            return;
        }

        await msg.ReplyAsync("ok");
    }

    private async ValueTask HandleRemoveBehaviourAsync(NatsSvcMsg<string> msg)
    {
        if (!TryParseRequest(msg.Data, out var entityId, out var behaviourName))
        {
            await msg.ReplyErrorAsync(400, "Expected format: entityId:behaviourName");
            return;
        }

        if (!_repo.Exists(entityId))
        {
            await msg.ReplyErrorAsync(404, "Entity not found");
            return;
        }

        if (!_repo.HasBehaviour(entityId, behaviourName))
        {
            await msg.ReplyErrorAsync(404, "Behaviour not found on entity");
            return;
        }

        // Request the module runtime to destroy the worker for this behaviour.
        try
        {
            var workerReply = await _nats.RequestAsync<string, string>(
                $"worker.remove.{behaviourName}",
                entityId.Value.ToString()
            );

            if (workerReply.Data is not "ok")
            {
                await msg.ReplyErrorAsync(502, $"Module runtime error: {workerReply.Data}");
                return;
            }
        }
        catch (NatsNoRespondersException)
        {
            await msg.ReplyErrorAsync(502, "No module handles this behaviour");
            return;
        }

        if (!_repo.RemoveBehaviour(entityId, behaviourName))
        {
            await msg.ReplyErrorAsync(404, "Behaviour not found on entity");
            return;
        }

        await msg.ReplyAsync("ok");
    }

    private async ValueTask HandleHasBehaviourAsync(NatsSvcMsg<string> msg)
    {
        if (!TryParseRequest(msg.Data, out var entityId, out var behaviourName))
        {
            await msg.ReplyErrorAsync(400, "Expected format: entityId:behaviourName");
            return;
        }

        if (!_repo.Exists(entityId))
        {
            await msg.ReplyErrorAsync(404, "Entity not found");
            return;
        }

        var has = _repo.HasBehaviour(entityId, behaviourName);
        await msg.ReplyAsync(has ? "true" : "false");
    }

    private async ValueTask HandleListBehavioursAsync(NatsSvcMsg<string> msg)
    {
        if (!Guid.TryParse(msg.Data, out var guid))
        {
            await msg.ReplyErrorAsync(400, "Invalid EntityId format");
            return;
        }

        var entityId = new EntityId(guid);

        if (!_repo.Exists(entityId))
        {
            await msg.ReplyErrorAsync(404, "Entity not found");
            return;
        }

        var names = _repo.ListBehaviours(entityId);
        await msg.ReplyAsync(string.Join(",", names));
    }

    private static bool TryParseRequest(
        string? data,
        out EntityId entityId,
        out string behaviourName
    )
    {
        entityId = default;
        behaviourName = string.Empty;

        if (string.IsNullOrEmpty(data))
            return false;

        var sep = data.IndexOf(':');
        if (sep < 0)
            return false;

        if (!Guid.TryParse(data.AsSpan(0, sep), out var guid))
            return false;

        behaviourName = data[(sep + 1)..];
        if (string.IsNullOrWhiteSpace(behaviourName))
            return false;

        entityId = new EntityId(guid);
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        await _svc.DisposeAsync();
    }
}
