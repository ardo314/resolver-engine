using Engine.Core;
using NATS.Client.Core;
using NATS.Client.Services;

namespace Engine.Backend;

/// <summary>
/// Manages entity lifecycles and component tracking, exposed as a NATS service.
///
/// When adding or removing components, the backend first sends a NATS request to the
/// module runtime (worker.create / worker.remove) and only commits to the entity
/// registry if the runtime responds successfully.
///
/// NATS subjects (under the "entity" service group):
///   entity.create           – request with empty payload, replies with the new EntityId (Guid string).
///   entity.destroy          – request with EntityId (Guid string), replies with "ok" or an error.
///   entity.exists           – request with EntityId (Guid string), replies with "true" / "false".
///   entity.list             – request with empty payload, replies with comma-separated EntityId list.
///   entity.add-component    – request "entityId:componentName", replies "ok" or error.
///   entity.remove-component – request "entityId:componentName", replies "ok" or error.
///   entity.has-component    – request "entityId:componentName", replies "true" / "false".
///   entity.list-components  – request EntityId (Guid string), replies comma-separated names.
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

        // Component management
        await grp.AddEndpointAsync<string>(name: "add-component", handler: HandleAddComponentAsync);
        await grp.AddEndpointAsync<string>(
            name: "remove-component",
            handler: HandleRemoveComponentAsync
        );
        await grp.AddEndpointAsync<string>(name: "has-component", handler: HandleHasComponentAsync);
        await grp.AddEndpointAsync<string>(
            name: "list-components",
            handler: HandleListComponentsAsync
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
        var components = _repo.Destroy(id);

        if (components is null)
        {
            await msg.ReplyErrorAsync(404, "Entity not found");
            return;
        }

        // Tear down all workers that were instantiated for this entity.
        foreach (var componentName in components)
        {
            try
            {
                await _nats.RequestAsync<string, string>(
                    $"worker.remove.{componentName}",
                    id.Value.ToString()
                );
            }
            catch (NatsNoRespondersException)
            {
                // Module is gone – nothing to clean up on the runtime side.
            }
        }

        await msg.ReplyAsync("ok");
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

    // ── Component management ────────────────────────────────────────────

    private async ValueTask HandleAddComponentAsync(NatsSvcMsg<string> msg)
    {
        if (!TryParseRequest(msg.Data, out var entityId, out var componentName))
        {
            await msg.ReplyErrorAsync(400, "Expected format: entityId:componentName");
            return;
        }

        if (!_repo.Exists(entityId))
        {
            await msg.ReplyErrorAsync(404, "Entity not found");
            return;
        }

        if (_repo.HasComponent(entityId, componentName))
        {
            await msg.ReplyErrorAsync(409, "Component already added");
            return;
        }

        // Request the module runtime to create a worker for this component.
        try
        {
            var workerReply = await _nats.RequestAsync<string, string>(
                $"worker.create.{componentName}",
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
            await msg.ReplyErrorAsync(502, "No module handles this component");
            return;
        }

        if (!_repo.AddComponent(entityId, componentName))
        {
            await msg.ReplyErrorAsync(409, "Component already added");
            return;
        }

        await msg.ReplyAsync("ok");
    }

    private async ValueTask HandleRemoveComponentAsync(NatsSvcMsg<string> msg)
    {
        if (!TryParseRequest(msg.Data, out var entityId, out var componentName))
        {
            await msg.ReplyErrorAsync(400, "Expected format: entityId:componentName");
            return;
        }

        if (!_repo.Exists(entityId))
        {
            await msg.ReplyErrorAsync(404, "Entity not found");
            return;
        }

        if (!_repo.HasComponent(entityId, componentName))
        {
            await msg.ReplyErrorAsync(404, "Component not found on entity");
            return;
        }

        // Request the module runtime to destroy the worker for this component.
        try
        {
            var workerReply = await _nats.RequestAsync<string, string>(
                $"worker.remove.{componentName}",
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
            await msg.ReplyErrorAsync(502, "No module handles this component");
            return;
        }

        if (!_repo.RemoveComponent(entityId, componentName))
        {
            await msg.ReplyErrorAsync(404, "Component not found on entity");
            return;
        }

        await msg.ReplyAsync("ok");
    }

    private async ValueTask HandleHasComponentAsync(NatsSvcMsg<string> msg)
    {
        if (!TryParseRequest(msg.Data, out var entityId, out var componentName))
        {
            await msg.ReplyErrorAsync(400, "Expected format: entityId:componentName");
            return;
        }

        if (!_repo.Exists(entityId))
        {
            await msg.ReplyErrorAsync(404, "Entity not found");
            return;
        }

        var has = _repo.HasComponent(entityId, componentName);
        await msg.ReplyAsync(has ? "true" : "false");
    }

    private async ValueTask HandleListComponentsAsync(NatsSvcMsg<string> msg)
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

        var names = _repo.ListComponents(entityId);
        await msg.ReplyAsync(string.Join(",", names));
    }

    private static bool TryParseRequest(
        string? data,
        out EntityId entityId,
        out string componentName
    )
    {
        entityId = default;
        componentName = string.Empty;

        if (string.IsNullOrEmpty(data))
            return false;

        var sep = data.IndexOf(':');
        if (sep < 0)
            return false;

        if (!Guid.TryParse(data.AsSpan(0, sep), out var guid))
            return false;

        componentName = data[(sep + 1)..];
        if (string.IsNullOrWhiteSpace(componentName))
            return false;

        entityId = new EntityId(guid);
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        await _svc.DisposeAsync();
    }
}
