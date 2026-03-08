using Engine.Core;

namespace Engine.Backend;

/// <summary>
/// Server-side entity implementation backed by an <see cref="EntityRecord"/>.
/// </summary>
public sealed class Entity : IEntity
{
    private readonly EntityRecord _record;

    public Entity(EntityRecord record)
    {
        _record = record;
    }

    public EntityId Id => _record.Id;

    public Task AddComponentAsync<T>(CancellationToken ct)
        where T : IComponent
    {
        _record.AddComponent(Activator.CreateInstance<T>());
        return Task.CompletedTask;
    }

    public Task RemoveComponentAsync<T>(CancellationToken ct)
        where T : IComponent
    {
        _record.RemoveComponent<T>();
        return Task.CompletedTask;
    }

    public Task<T> GetComponentAsync<T>(CancellationToken ct)
        where T : IComponent
    {
        return Task.FromResult(_record.GetComponent<T>());
    }

    public Task<bool> HasComponentAsync<T>(CancellationToken ct)
        where T : IComponent
    {
        return Task.FromResult(_record.HasComponent<T>());
    }
}
