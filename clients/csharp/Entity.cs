using System.Text.Json;
using Engine.Core;
using NATS.Client.Core;

namespace Engine.Client;

public class Entity
{
    private readonly INatsConnection _nc;
    public EntityId Id { get; }

    public Entity(INatsConnection nc, EntityId id)
    {
        _nc = nc;
        Id = id;
    }

    public async Task AddComponent(Component component, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new { entityId = Id.Value, componentId = component.Id });
        var reply = await _nc.RequestAsync<string, string>(Subjects.AddComponent, payload, cancellationToken: ct);
        using var doc = JsonDocument.Parse(reply.Data!);
        if (doc.RootElement.TryGetProperty("error", out var err))
            throw new InvalidOperationException(err.GetString());
    }

    public async Task RemoveComponent(Component component, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new { entityId = Id.Value, componentId = component.Id });
        await _nc.RequestAsync<string, string>(Subjects.RemoveComponent, payload, cancellationToken: ct);
    }

    public async Task<bool> HasComponent(Component component, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new { entityId = Id.Value, componentId = component.Id });
        var reply = await _nc.RequestAsync<string, string>(Subjects.HasComponent, payload, cancellationToken: ct);
        return reply.Data == "true";
    }

    public async Task<ComponentEntry[]> GetComponentEntries(CancellationToken ct = default)
    {
        var reply = await _nc.RequestAsync<string, string>(Subjects.GetComponents, Id.Value, cancellationToken: ct);
        using var doc = JsonDocument.Parse(reply.Data!);
        return doc.RootElement.EnumerateArray().Select(el => new ComponentEntry(
            el.GetProperty("componentId").GetString()!,
            el.GetProperty("methodNames").EnumerateArray().Select(m => m.GetString()!).ToList()
        )).ToArray();
    }

    public async Task<Dictionary<string, string>?> Query(Query query, CancellationToken ct = default)
    {
        var methodNames = query.Methods.Select(m => m.Name).ToList();
        var payload = JsonSerializer.Serialize(new { entityId = Id.Value, methodNames });
        var reply = await _nc.RequestAsync<string, string>(Subjects.QueryEntity, payload, cancellationToken: ct);
        using var doc = JsonDocument.Parse(reply.Data!);
        var root = doc.RootElement;

        if (!root.GetProperty("match").GetBoolean())
            return null;

        return root.GetProperty("methods").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetString()!);
    }

    /// <summary>
    /// Call a method on a component attached to this entity.
    /// </summary>
    public async Task<JsonElement?> CallMethod(
        string componentId,
        string methodName,
        JsonElement? input = null,
        CancellationToken ct = default)
    {
        var subject = WorkerSubjects.CallMethod(componentId, Id.Value, methodName);
        var payload = input.HasValue
            ? JsonSerializer.Serialize(new { input = input.Value })
            : null;
        var reply = await _nc.RequestAsync<string, string>(subject, payload ?? "", cancellationToken: ct);
        using var doc = JsonDocument.Parse(reply.Data!);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var err))
            throw new InvalidOperationException(err.GetString());

        if (root.TryGetProperty("result", out var result))
            return result.Clone();

        return null;
    }
}

public record ComponentEntry(string ComponentId, IReadOnlyList<string> MethodNames);
