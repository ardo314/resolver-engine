using System.Text.Json;
using Engine.Core;
using NATS.Client.Core;

namespace Engine.Client;

public record RegisteredComponent(
    string ComponentId,
    IReadOnlyList<string> MethodNames,
    JsonElement Schema
);

public class World
{
    private readonly INatsConnection _nc;

    public World(INatsConnection nc) => _nc = nc;

    public async Task<Entity> CreateEntity(CancellationToken ct = default)
    {
        var reply = await _nc.RequestAsync<string, string>(Subjects.CreateEntity, "", cancellationToken: ct);
        var id = new EntityId(reply.Data!);
        return new Entity(_nc, id);
    }

    public async Task<bool> DeleteEntity(EntityId id, CancellationToken ct = default)
    {
        var reply = await _nc.RequestAsync<string, string>(Subjects.DeleteEntity, id.Value, cancellationToken: ct);
        return reply.Data == "true";
    }

    public async Task<bool> HasEntity(EntityId id, CancellationToken ct = default)
    {
        var reply = await _nc.RequestAsync<string, string>(Subjects.HasEntity, id.Value, cancellationToken: ct);
        return reply.Data == "true";
    }

    public async Task<Entity[]> ListEntities(CancellationToken ct = default)
    {
        var reply = await _nc.RequestAsync<string, string>(Subjects.ListEntities, "", cancellationToken: ct);
        var ids = JsonSerializer.Deserialize<string[]>(reply.Data!)!;
        return ids.Select(id => new Entity(_nc, new EntityId(id))).ToArray();
    }

    public async Task<RegisteredComponent[]> ListComponents(CancellationToken ct = default)
    {
        var reply = await _nc.RequestAsync<string, string>(Subjects.ListComponents, "", cancellationToken: ct);
        using var doc = JsonDocument.Parse(reply.Data!);
        return doc.RootElement.EnumerateArray().Select(el => new RegisteredComponent(
            el.GetProperty("componentId").GetString()!,
            el.GetProperty("methodNames").EnumerateArray().Select(m => m.GetString()!).ToList(),
            el.GetProperty("schema").Clone()
        )).ToArray();
    }

    public async Task AddComponentById(EntityId entityId, string componentId, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new { entityId = entityId.Value, componentId });
        var reply = await _nc.RequestAsync<string, string>(Subjects.AddComponent, payload, cancellationToken: ct);
        using var doc = JsonDocument.Parse(reply.Data!);
        if (doc.RootElement.TryGetProperty("error", out var err))
            throw new InvalidOperationException(err.GetString());
    }

    public async Task RemoveComponentById(EntityId entityId, string componentId, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new { entityId = entityId.Value, componentId });
        await _nc.RequestAsync<string, string>(Subjects.RemoveComponent, payload, cancellationToken: ct);
    }
}
