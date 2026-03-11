using System.ComponentModel;
using System.Numerics;
using Engine.Core;
using Engine.Math;

namespace Modules.InMemoryPose;

public partial class InMemoryPose : Component, IPose
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
