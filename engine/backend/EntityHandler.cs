using System.Text.Json;
using Engine.Core;
using NATS.Client.Core;

namespace Engine.Backend;

public record RegisteredComponent(IReadOnlyList<string> MethodNames, JsonElement Schema);

public class EntityHandler
{
    private readonly EntityRepository _repo = new();
    private readonly Dictionary<string, RegisteredComponent> _components = new();
    private readonly INatsConnection _nc;
    private readonly CancellationToken _ct;

    public EntityHandler(INatsConnection nc, CancellationToken ct = default)
    {
        _nc = nc;
        _ct = ct;
    }

    public async Task ListenAsync()
    {
        var tasks = new[]
        {
            HandleRegisterComponent(),
            HandleListComponents(),
            HandleCreateEntity(),
            HandleDeleteEntity(),
            HandleHasEntity(),
            HandleListEntities(),
            HandleAddComponent(),
            HandleRemoveComponent(),
            HandleHasComponent(),
            HandleGetComponents(),
            HandleQueryEntity(),
        };

        await Task.WhenAll(tasks);
    }

    private async Task HandleRegisterComponent()
    {
        await foreach (var msg in _nc.SubscribeAsync<string>(Subjects.RegisterComponent, cancellationToken: _ct))
        {
            try
            {
                using var doc = JsonDocument.Parse(msg.Data!);
                var root = doc.RootElement;
                var componentId = root.GetProperty("componentId").GetString()!;
                var methodNames = root.GetProperty("methodNames")
                    .EnumerateArray()
                    .Select(e => e.GetString()!)
                    .ToList();
                var schema = root.GetProperty("schema").Clone();

                _components[componentId] = new RegisteredComponent(methodNames, schema);
                await msg.ReplyAsync(JsonSerializer.Serialize(new { ok = true }), cancellationToken: _ct);
            }
            catch (Exception e)
            {
                await msg.ReplyAsync(JsonSerializer.Serialize(new { error = e.Message }), cancellationToken: _ct);
            }
        }
    }

    private async Task HandleListComponents()
    {
        await foreach (var msg in _nc.SubscribeAsync<string>(Subjects.ListComponents, cancellationToken: _ct))
        {
            var entries = _components.Select(kvp => new
            {
                componentId = kvp.Key,
                methodNames = kvp.Value.MethodNames,
                schema = kvp.Value.Schema,
            });
            await msg.ReplyAsync(JsonSerializer.Serialize(entries), cancellationToken: _ct);
        }
    }

    private async Task HandleCreateEntity()
    {
        await foreach (var msg in _nc.SubscribeAsync<string>(Subjects.CreateEntity, cancellationToken: _ct))
        {
            var id = _repo.Create();
            await msg.ReplyAsync(id.Value, cancellationToken: _ct);
        }
    }

    private async Task HandleDeleteEntity()
    {
        await foreach (var msg in _nc.SubscribeAsync<string>(Subjects.DeleteEntity, cancellationToken: _ct))
        {
            var id = new EntityId(msg.Data!);
            var componentIds = _repo.GetComponentIds(id);
            foreach (var componentId in componentIds)
            {
                await _nc.PublishAsync(Subjects.StopWorker,
                    JsonSerializer.Serialize(new { entityId = id.Value, componentId }),
                    cancellationToken: _ct);
            }
            var result = _repo.Delete(id);
            await msg.ReplyAsync(result.ToString().ToLowerInvariant(), cancellationToken: _ct);
        }
    }

    private async Task HandleHasEntity()
    {
        await foreach (var msg in _nc.SubscribeAsync<string>(Subjects.HasEntity, cancellationToken: _ct))
        {
            var id = new EntityId(msg.Data!);
            var result = _repo.Has(id);
            await msg.ReplyAsync(result.ToString().ToLowerInvariant(), cancellationToken: _ct);
        }
    }

    private async Task HandleListEntities()
    {
        await foreach (var msg in _nc.SubscribeAsync<string>(Subjects.ListEntities, cancellationToken: _ct))
        {
            var ids = _repo.GetAll().Select(id => id.Value);
            await msg.ReplyAsync(JsonSerializer.Serialize(ids), cancellationToken: _ct);
        }
    }

    private async Task HandleAddComponent()
    {
        await foreach (var msg in _nc.SubscribeAsync<string>(Subjects.AddComponent, cancellationToken: _ct))
        {
            try
            {
                using var doc = JsonDocument.Parse(msg.Data!);
                var root = doc.RootElement;
                var entityId = new EntityId(root.GetProperty("entityId").GetString()!);
                var componentId = root.GetProperty("componentId").GetString()!;

                if (!_components.ContainsKey(componentId))
                {
                    await msg.ReplyAsync(
                        JsonSerializer.Serialize(new { error = $"No worker registered for component {componentId}" }),
                        cancellationToken: _ct);
                    continue;
                }

                _repo.AddComponent(entityId, componentId);

                await _nc.PublishAsync(Subjects.StartWorker,
                    JsonSerializer.Serialize(new { entityId = entityId.Value, componentId }),
                    cancellationToken: _ct);

                await msg.ReplyAsync(JsonSerializer.Serialize(new { ok = true }), cancellationToken: _ct);
            }
            catch (Exception e)
            {
                await msg.ReplyAsync(JsonSerializer.Serialize(new { error = e.Message }), cancellationToken: _ct);
            }
        }
    }

    private async Task HandleRemoveComponent()
    {
        await foreach (var msg in _nc.SubscribeAsync<string>(Subjects.RemoveComponent, cancellationToken: _ct))
        {
            try
            {
                using var doc = JsonDocument.Parse(msg.Data!);
                var root = doc.RootElement;
                var entityId = new EntityId(root.GetProperty("entityId").GetString()!);
                var componentId = root.GetProperty("componentId").GetString()!;

                var result = _repo.RemoveComponent(entityId, componentId);

                await _nc.PublishAsync(Subjects.StopWorker,
                    JsonSerializer.Serialize(new { entityId = entityId.Value, componentId }),
                    cancellationToken: _ct);

                await msg.ReplyAsync(result.ToString().ToLowerInvariant(), cancellationToken: _ct);
            }
            catch (Exception e)
            {
                await msg.ReplyAsync(JsonSerializer.Serialize(new { error = e.Message }), cancellationToken: _ct);
            }
        }
    }

    private async Task HandleHasComponent()
    {
        await foreach (var msg in _nc.SubscribeAsync<string>(Subjects.HasComponent, cancellationToken: _ct))
        {
            using var doc = JsonDocument.Parse(msg.Data!);
            var root = doc.RootElement;
            var entityId = new EntityId(root.GetProperty("entityId").GetString()!);
            var componentId = root.GetProperty("componentId").GetString()!;

            var result = _repo.HasComponent(entityId, componentId);
            await msg.ReplyAsync(result.ToString().ToLowerInvariant(), cancellationToken: _ct);
        }
    }

    private async Task HandleGetComponents()
    {
        await foreach (var msg in _nc.SubscribeAsync<string>(Subjects.GetComponents, cancellationToken: _ct))
        {
            try
            {
                var entityId = new EntityId(msg.Data!);
                var componentIds = _repo.GetComponentIds(entityId);
                var entries = componentIds.Select(id =>
                {
                    var methodNames = _components.TryGetValue(id, out var reg)
                        ? reg.MethodNames
                        : (IReadOnlyList<string>)Array.Empty<string>();
                    return new { componentId = id, methodNames };
                });
                await msg.ReplyAsync(JsonSerializer.Serialize(entries), cancellationToken: _ct);
            }
            catch (Exception e)
            {
                await msg.ReplyAsync(JsonSerializer.Serialize(new { error = e.Message }), cancellationToken: _ct);
            }
        }
    }

    private async Task HandleQueryEntity()
    {
        await foreach (var msg in _nc.SubscribeAsync<string>(Subjects.QueryEntity, cancellationToken: _ct))
        {
            try
            {
                using var doc = JsonDocument.Parse(msg.Data!);
                var root = doc.RootElement;
                var entityId = new EntityId(root.GetProperty("entityId").GetString()!);
                var methodNames = root.GetProperty("methodNames")
                    .EnumerateArray()
                    .Select(e => e.GetString()!)
                    .ToList();

                var componentIds = _repo.GetComponentIds(entityId);

                // Build method → componentId mapping across all components on entity
                var methodMap = new Dictionary<string, string>();
                foreach (var compId in componentIds)
                {
                    if (!_components.TryGetValue(compId, out var reg)) continue;
                    foreach (var methodName in reg.MethodNames)
                    {
                        // First component wins for a given method name
                        methodMap.TryAdd(methodName, compId);
                    }
                }

                var missing = methodNames.Where(m => !methodMap.ContainsKey(m)).ToList();
                if (missing.Count > 0)
                {
                    await msg.ReplyAsync(
                        JsonSerializer.Serialize(new { match = false, missing }),
                        cancellationToken: _ct);
                }
                else
                {
                    var methods = methodNames.ToDictionary(m => m, m => methodMap[m]);
                    await msg.ReplyAsync(
                        JsonSerializer.Serialize(new { match = true, methods }),
                        cancellationToken: _ct);
                }
            }
            catch (Exception e)
            {
                await msg.ReplyAsync(JsonSerializer.Serialize(new { error = e.Message }), cancellationToken: _ct);
            }
        }
    }
}
