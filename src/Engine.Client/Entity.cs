using Engine.Core;
using NATS.Client.Core;

namespace Engine.Client;

/// <summary>
/// Client-side proxy for a single entity.
/// Component operations are forwarded to the backend over NATS request-reply.
/// </summary>
public sealed class Entity
{
    private readonly INatsConnection _nats;

    public EntityId Id { get; }

    /// <summary>
    /// The NATS connection used by this entity for component proxies.
    /// </summary>
    public INatsConnection Nats => _nats;

    public Entity(EntityId id, INatsConnection nats)
    {
        Id = id;
        _nats = nats;
    }

    /// <summary>
    /// Registers a component on this entity via the backend.
    /// The marker struct <typeparamref name="TComponent"/> must carry <c>[HasBehaviour&lt;...&gt;]</c>
    /// attributes indicating which component interfaces it provides.
    /// </summary>
    public async Task AddComponentAsync<TComponent>(CancellationToken ct = default)
        where TComponent : struct, IComponent
    {
        var payload = $"{Id.Value}:{typeof(TComponent).Name}";
        var reply = await _nats.RequestAsync<string, string>(
            "entity.add-component",
            payload,
            cancellationToken: ct
        );

        if (reply.Data is not "ok")
            throw new InvalidOperationException(
                $"Failed to add component {typeof(TComponent).Name} to entity {Id}: {reply.Data}"
            );
    }

    /// <summary>
    /// Removes a component from this entity via the backend.
    /// </summary>
    public async Task RemoveComponentAsync<TComponent>(CancellationToken ct = default)
        where TComponent : struct, IComponent
    {
        var payload = $"{Id.Value}:{typeof(TComponent).Name}";
        var reply = await _nats.RequestAsync<string, string>(
            "entity.remove-component",
            payload,
            cancellationToken: ct
        );

        if (reply.Data is not "ok")
            throw new InvalidOperationException(
                $"Failed to remove component {typeof(TComponent).Name} from entity {Id}: {reply.Data}"
            );
    }

    /// <summary>
    /// Checks whether this entity has a given component via the backend.
    /// </summary>
    public async Task<bool> HasComponentAsync<TComponent>(CancellationToken ct = default)
        where TComponent : struct, IComponent
    {
        var payload = $"{Id.Value}:{typeof(TComponent).Name}";
        var reply = await _nats.RequestAsync<string, string>(
            "entity.has-component",
            payload,
            cancellationToken: ct
        );

        return reply.Data is "true";
    }

    /// <summary>
    /// Lists all component names attached to this entity.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListComponentsAsync(CancellationToken ct = default)
    {
        var reply = await _nats.RequestAsync<string, string>(
            "entity.list-components",
            Id.Value.ToString(),
            cancellationToken: ct
        );

        if (string.IsNullOrEmpty(reply.Data))
            return Array.Empty<string>();

        return reply.Data!.Split(',');
    }

    /// <summary>
    /// Returns a client-side proxy instance of the given type.
    /// Works with both behaviour proxies (e.g. <c>PoseProxy</c>) and component proxies
    /// (e.g. <c>InMemoryPoseProxy</c>). All generated proxy types implement <see cref="IProxy"/>.
    /// </summary>
    public T GetComponentProxy<T>()
        where T : class, IProxy
    {
        return (T)Activator.CreateInstance(typeof(T), Id, _nats)!;
    }
}
