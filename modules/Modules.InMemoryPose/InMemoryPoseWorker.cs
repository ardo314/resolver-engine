using System.Numerics;
using Engine.Core;
using Engine.Module;

namespace Modules.InMemoryPose;

public partial class InMemoryPoseWorker : BehaviourWorker<IPose>
{
    public Task<Pose> GetDataAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task SetDataAsync(Pose data, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
