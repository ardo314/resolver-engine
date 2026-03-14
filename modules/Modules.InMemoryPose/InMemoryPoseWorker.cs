using System.ComponentModel;
using System.Numerics;
using Engine.Core;

namespace Modules.InMemoryPose;

public partial class InMemoryPoseWorker : ComponentWorker<IPose>
{
    public Task InitDataAsync(Pose data, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<Pose> GetDataAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task SetDataAsync(Pose data, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
