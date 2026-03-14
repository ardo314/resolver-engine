using Engine.Core;
using NATS.Client.Core;

namespace Engine.Module;

/// <summary>
/// Client-side proxy for a single entity.
/// Behaviour operations are forwarded to the backend over NATS request-reply.
/// </summary>
public sealed class Entity
{
    private readonly INatsConnection _nats;

    public EntityId Id { get; }

    public Entity(EntityId id, INatsConnection nats)
    {
        Id = id;
        _nats = nats;
    }

    /// <summary>
    /// Registers a behaviour on this entity via the backend.
    /// </summary>
    public async Task AddBehaviourAsync<TBehaviour>(CancellationToken ct = default)
        where TBehaviour : IBehaviour
    {
        var payload = $"{Id.Value}:{BehaviourName<TBehaviour>()}";
        var reply = await _nats.RequestAsync<string, string>(
            "entity.add-behaviour",
            payload,
            cancellationToken: ct
        );

        if (reply.Data is not "ok")
            throw new InvalidOperationException(
                $"Failed to add behaviour {BehaviourName<TBehaviour>()} to entity {Id}: {reply.Data}"
            );
    }

    /// <summary>
    /// Removes a behaviour from this entity via the backend.
    /// </summary>
    public async Task RemoveBehaviourAsync<TBehaviour>(CancellationToken ct = default)
        where TBehaviour : IBehaviour
    {
        var payload = $"{Id.Value}:{BehaviourName<TBehaviour>()}";
        var reply = await _nats.RequestAsync<string, string>(
            "entity.remove-behaviour",
            payload,
            cancellationToken: ct
        );

        if (reply.Data is not "ok")
            throw new InvalidOperationException(
                $"Failed to remove behaviour {BehaviourName<TBehaviour>()} from entity {Id}: {reply.Data}"
            );
    }

    /// <summary>
    /// Checks whether this entity has a given behaviour via the backend.
    /// </summary>
    public async Task<bool> HasBehaviourAsync<TBehaviour>(CancellationToken ct = default)
        where TBehaviour : IBehaviour
    {
        var payload = $"{Id.Value}:{BehaviourName<TBehaviour>()}";
        var reply = await _nats.RequestAsync<string, string>(
            "entity.has-behaviour",
            payload,
            cancellationToken: ct
        );

        return reply.Data is "true";
    }

    /// <summary>
    /// Lists all behaviour names attached to this entity.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListBehavioursAsync(CancellationToken ct = default)
    {
        var reply = await _nats.RequestAsync<string, string>(
            "entity.list-behaviours",
            Id.Value.ToString(),
            cancellationToken: ct
        );

        if (string.IsNullOrEmpty(reply.Data))
            return Array.Empty<string>();

        return reply.Data!.Split(',');
    }

    /// <summary>
    /// Derives the canonical behaviour name from the type parameter.
    /// </summary>
    private static string BehaviourName<TBehaviour>()
        where TBehaviour : IBehaviour => typeof(TBehaviour).Name;
}
