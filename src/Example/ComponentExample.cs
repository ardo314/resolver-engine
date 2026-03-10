using System.Numerics;
using Engine.Core;
using Engine.Math;
using Modules.DatabasePose;
using Modules.InMemoryPose;

namespace Example;

public class SomeUserPlugin : Plugin
{
    public override async Task OnStartAsync(CancellationToken ct = default)
    {
        var entity = await World.CreateEntityAsync(ct);

        // Entity-level lifecycle events
        entity.ComponentAdded += (component) =>
        {
            Console.WriteLine($"Component {component.GetType().Name} added to entity {entity}");
        };
        entity.ComponentRemoved += (component) =>
        {
            Console.WriteLine($"Component {component.GetType().Name} removed from entity {entity}");
        };

        // Adding components — must use concrete type (creates a per-entity instance)
        var pose = await entity.AddComponentAsync<InMemoryPose, Pose>(
            new Pose { Position = Vector3.Zero, Rotation = Quaternion.Identity },
            ct
        );

        // Getting component data — either interface or concrete type
        var pose2 = await entity.GetComponentAsync<IPose, Pose>(ct);
        var pose3 = await entity.GetComponentAsync<InMemoryPose, Pose>(ct);

        // DatabasePose was never added, so this returns null
        var pose4 = await entity.GetComponentAsync<DatabasePose, Pose>(ct);

        if (pose2 is not null)
        {
            pose2.Updated += (data) =>
            {
                Console.WriteLine($"Entity {entity} pose updated: {data}");
            };
            pose2.Removed += () =>
            {
                Console.WriteLine($"Entity {entity} pose removed");
            };

            var poseData = await pose2.GetAsync(ct);

            await pose2.SetAsync(
                new Pose
                {
                    Position = new Vector3(1, 2, 3),
                    Rotation = Quaternion.CreateFromYawPitchRoll(0.1f, 0.2f, 0.3f),
                },
                ct
            );
        }

        await entity.HasComponentAsync<IPose>(ct); // true
        await entity.HasComponentAsync<InMemoryPose>(ct); // true
        await entity.HasComponentAsync<DatabasePose>(ct); // false

        // Removing components — either interface or concrete type works
        await entity.RemoveComponentAsync<IPose>(ct);

        await World.DestroyEntityAsync(entity, ct);
    }

    public override Task OnStopAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
