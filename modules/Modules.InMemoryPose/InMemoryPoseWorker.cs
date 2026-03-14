using System.Numerics;
using Engine.Core;
using Engine.Module;

namespace Modules.InMemoryPose;

public partial class InMemoryPoseWorker : BehaviourWorker<IPose>
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
