using System.Numerics;
using Engine.Core;
using Engine.Math;

namespace Modules.DatabasePose;

/// <summary>
/// Behaviour contract for database operations — referenced by DatabasePose.
/// </summary>
public interface IDatabase : IBehaviour
{
    Task CreateRecordAsync(Entity entity, Pose data, CancellationToken ct = default);
    Task DeleteRecordAsync(Entity entity, CancellationToken ct = default);
}

public partial class DatabasePose : Component<IPose>
{
    public async Task OnAddAsync(Pose initialData, CancellationToken ct = default)
    {
        var database = await Entity.GetBehaviourAsync<IDatabase>();
        await database.CreateRecordAsync(Entity, initialData, ct);
        Console.WriteLine($"Entity {Entity} added to DatabasePose component.");
    }

    public async Task OnRemoveAsync(CancellationToken ct = default)
    {
        var database = await Entity.GetBehaviourAsync<IDatabase>();
        await database.DeleteRecordAsync(Entity, ct);
        Console.WriteLine($"Entity {Entity} removed from DatabasePose component.");
    }

    public Task<Pose> GetAsync(CancellationToken ct = default)
    {
        var data = new Pose
        {
            Position = new Vector3(1, 2, 3),
            Rotation = Quaternion.CreateFromYawPitchRoll(0.1f, 0.2f, 0.3f),
        };
        return Task.FromResult(data);
    }

    public Task SetAsync(Pose data, CancellationToken ct = default)
    {
        Console.WriteLine(
            $"Setting pose for entity {Entity} to position {data.Position} and rotation {data.Rotation}"
        );
        RaiseUpdated(data);
        return Task.CompletedTask;
    }
}
