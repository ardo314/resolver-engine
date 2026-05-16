using Engine.Core;

namespace Engine.Backend;

public class EntityRepository
{
    private readonly HashSet<EntityId> _entities = new();
    private readonly Dictionary<EntityId, HashSet<string>> _components = new();
    private int _nextId;

    public EntityId Create()
    {
        var id = new EntityId($"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{_nextId++}");
        _entities.Add(id);
        _components[id] = new HashSet<string>();
        return id;
    }

    public bool Delete(EntityId id)
    {
        _components.Remove(id);
        return _entities.Remove(id);
    }

    public bool Has(EntityId id) => _entities.Contains(id);

    public IReadOnlySet<EntityId> GetAll() => _entities;

    public void AddComponent(EntityId entityId, string componentId)
    {
        if (!_components.TryGetValue(entityId, out var set))
            throw new InvalidOperationException($"Entity {entityId} does not exist");
        if (!set.Add(componentId))
            throw new InvalidOperationException(
                $"Component {componentId} already exists on entity {entityId}");
    }

    public bool RemoveComponent(EntityId entityId, string componentId)
    {
        if (!_components.TryGetValue(entityId, out var set))
            return false;
        return set.Remove(componentId);
    }

    public bool HasComponent(EntityId entityId, string componentId)
    {
        return _components.TryGetValue(entityId, out var set) && set.Contains(componentId);
    }

    public IReadOnlyList<string> GetComponentIds(EntityId entityId)
    {
        if (!_components.TryGetValue(entityId, out var set))
            return Array.Empty<string>();
        return set.ToList();
    }
}
