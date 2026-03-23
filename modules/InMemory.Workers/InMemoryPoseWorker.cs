using System.Numerics;
using Engine.Client;
using Engine.Worker;
using InMemory;

namespace InMemory.Workers;

public partial class InMemoryPoseWorker : ComponentWorker<InMemoryPose>
{
    private Pose _pose = new Pose { Position = Vector3.Zero, Rotation = Quaternion.Identity };

    public Task<Pose> GetDataAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_pose);
    }

    public Task SetDataAsync(Pose data, CancellationToken ct = default)
    {
        _pose = data;
        return Task.CompletedTask;
    }
}
