using System.Numerics;

namespace Example;

public struct Pose
{
    public Vector3 Position { get; init; }
    public Quaternion Rotation { get; init; }
}

public interface IPose : IComponent<Pose> { }

public class InMemoryPose : IPose
{
    private readonly Dictionary<Entity, Pose> _poses = new();

    public Task OnAddAsync(Entity entity, Pose? initialData, CancellationToken ct = default)
    {
        _poses[entity] =
            initialData ?? new Pose { Position = Vector3.Zero, Rotation = Quaternion.Identity };
        return Task.CompletedTask;
    }

    public Task OnRemoveAsync(Entity entity, CancellationToken ct = default)
    {
        _poses.Remove(entity);
        return Task.CompletedTask;
    }

    public Task<Pose> GetAsync(Entity entity, CancellationToken ct = default)
    {
        if (!_poses.TryGetValue(entity, out var data))
        {
            data = new Pose { Position = Vector3.Zero, Rotation = Quaternion.Identity };
            _poses[entity] = data;
        }
        return Task.FromResult(data);
    }

    public Task SetAsync(Entity entity, Pose data, CancellationToken ct = default)
    {
        _poses[entity] = data;
        return Task.CompletedTask;
    }
}

public class DatabasePose : IPose
{
    public async Task OnAddAsync(Entity entity, Pose? initialData, CancellationToken ct = default)
    {
        var database = await entity.GetBehaviourAsync<IDatabase>();
        await database.CreateRecordAsync(entity, initialData, ct);
        // In a real implementation, this might create a record in the database for the entity's pose.
        Console.WriteLine($"Entity {entity} added to DatabasePose component.");
    }

    public async Task OnRemoveAsync(Entity entity, CancellationToken ct = default)
    {
        var database = await entity.GetBehaviourAsync<IDatabase>();
        await database.DeleteRecordAsync(entity, ct);
        // In a real implementation, this might delete the record from the database.
        Console.WriteLine($"Entity {entity} removed from DatabasePose component.");
    }

    // Imagine this connects to a database to get pose data for entities.
    public Task<Pose> GetAsync(Entity entity, CancellationToken ct = default)
    {
        // Placeholder implementation
        var data = new Pose
        {
            Position = new Vector3(1, 2, 3),
            Rotation = Quaternion.CreateFromYawPitchRoll(0.1f, 0.2f, 0.3f),
        };
        return Task.FromResult(data);
    }

    public Task SetAsync(Entity entity, Pose data, CancellationToken ct = default)
    {
        // Placeholder implementation - in a real system this would update the database.
        Console.WriteLine(
            $"Setting pose for entity {entity} to position {data.Position} and rotation {data.Rotation}"
        );
        return Task.CompletedTask;
    }
}

public class SomeUserPlugin : Plugin
{
    public async Task OnStartAsync()
    {
        // World is defined in plugin
        var entity = await world.CreateEntityAsync();

        // Adding components
        // Needs to be a specific implementation, can't use IPose
        var pose = await _entity.AddComponentAsync<InMemoryPose>();

        // This should fail, only one implementation of IPose can be added at a time
        await _entity.AddComponentAsync<DatabasePose>();

        // Getting component data should return the data, not the implementation
        // Either should work
        pose = await _entity.GetComponentAsync<IPose>();
        var pose2 = await _entity.GetComponentAsync<InMemoryPose>();

        // This should be null, as DatabasePose was not added successfully
        var pose3 = await _entity.GetComponentAsync<DatabasePose>();

        pose.Updated += (data) =>
        {
            Console.WriteLine($"Entity {entity} pose updated: {data}");
        };
        pose.Removed += () =>
        {
            Console.WriteLine($"Entity {entity} pose removed");
        };

        var poseData = await pose.GetAsync();

        await pose.SetAsync(
            new Pose
            {
                Position = new Vector3(1, 2, 3),
                Rotation = Quaternion.CreateFromYawPitchRoll(0.1f, 0.2f, 0.3f),
            }
        );

        await _entity.HasComponentAsync<IPose>(); // Should be true
        await _entity.HasComponentAsync<InMemoryPose>(); // Should be true
        await _entity.HasComponentAsync<DatabasePose>(); // Should be false

        // Removing components
        // Either should work without error, and remove the component, removing non-existent components should not error
        await _entity.RemoveComponentAsync<IPose>();
        await _entity.RemoveComponentAsync<InMemoryPose>();

        await world.DestroyEntityAsync(entity);
    }

    public Task OnStopAsync()
    {
        return Task.CompletedTask;
    }
}
