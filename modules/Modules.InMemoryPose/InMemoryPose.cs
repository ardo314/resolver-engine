using System.Numerics;
using Engine.Core;
using Engine.Math;

namespace Modules.InMemoryPose;

public partial class InMemoryPose : Component<IPose>
{
    private Pose _data;

    public Task OnAddAsync(Pose initialData, CancellationToken ct = default)
    {
        _data = initialData;
        return Task.CompletedTask;
    }

    public Task OnRemoveAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task<Pose> GetAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_data);
    }

    public Task SetAsync(Pose data, CancellationToken ct = default)
    {
        _data = data;
        RaiseUpdated(data);
        return Task.CompletedTask;
    }
}
