using System.Collections.Concurrent;
using Engine.Core;
using Engine.World.Contracts;

namespace Engine.World;

/// <summary>
/// Concrete implementation of the generated WorldServiceServerBase.
/// Hosts an in-process <see cref="Core.World"/> instance and handles
/// incoming NATS requests for entity and component management.
/// </summary>
public sealed class WorldService : WorldServiceServerBase
{
    private readonly Core.World _world = new();

    /// <summary>
    /// Maps entity ID strings back to their <see cref="EntityRef"/> handles.
    /// </summary>
    private readonly ConcurrentDictionary<string, EntityRef> _entities = new();

    public override Task<string> CreateEntity(CancellationToken cancellationToken)
    {
        var entityRef = _world.CreateEntity();
        var id = entityRef.Entity.Id.ToString();
        _entities[id] = entityRef;

        Console.WriteLine($"[World] Entity created: {id}");
        return Task.FromResult(id);
    }

    public override Task DestroyEntity(string entityId, CancellationToken cancellationToken)
    {
        if (_entities.TryRemove(entityId, out var entityRef))
        {
            _world.DestroyEntity(entityRef.Entity);
            Console.WriteLine($"[World] Entity destroyed: {entityId}");
        }
        else
        {
            Console.WriteLine($"[World] Entity not found: {entityId}");
        }

        return Task.CompletedTask;
    }

    public override Task<bool> AddComponent(
        string entityId,
        string componentType,
        CancellationToken cancellationToken
    )
    {
        if (!_entities.TryGetValue(entityId, out var entityRef))
        {
            Console.WriteLine($"[World] AddComponent failed — entity not found: {entityId}");
            return Task.FromResult(false);
        }

        var type = Type.GetType(componentType);
        if (type == null || !typeof(IComponent).IsAssignableFrom(type))
        {
            Console.WriteLine(
                $"[World] AddComponent failed — invalid component type: {componentType}"
            );
            return Task.FromResult(false);
        }

        var component = (IComponent)Activator.CreateInstance(type)!;
        _world.AddComponent(entityRef.Entity, component);

        Console.WriteLine($"[World] Component added: {type.Name} → {entityId}");
        return Task.FromResult(true);
    }

    public override Task<bool> HasComponent(
        string entityId,
        string componentType,
        CancellationToken cancellationToken
    )
    {
        if (!_entities.TryGetValue(entityId, out var entityRef))
            return Task.FromResult(false);

        var type = Type.GetType(componentType);
        if (type == null)
            return Task.FromResult(false);

        // Use reflection to call the generic FindComponent<T> method
        var method = typeof(Core.World)
            .GetMethod(
                "FindComponent",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            )!
            .MakeGenericMethod(type);

        var result = method.Invoke(_world, new object[] { entityRef.Entity });
        return Task.FromResult(result != null);
    }

    public override Task<bool> RemoveComponent(
        string entityId,
        string componentType,
        CancellationToken cancellationToken
    )
    {
        if (!_entities.TryGetValue(entityId, out var entityRef))
            return Task.FromResult(false);

        var type = Type.GetType(componentType);
        if (type == null)
            return Task.FromResult(false);

        var method = typeof(Core.World)
            .GetMethod(
                "RemoveComponent",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            )!
            .MakeGenericMethod(type);

        var result = (bool)method.Invoke(_world, new object[] { entityRef.Entity })!;

        if (result)
            Console.WriteLine($"[World] Component removed: {type.Name} ← {entityId}");

        return Task.FromResult(result);
    }
}
