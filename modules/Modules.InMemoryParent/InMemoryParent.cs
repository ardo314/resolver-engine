using Engine.Core;
using Engine.Hierarchy;

namespace Modules.InMemoryParent;

public partial class InMemoryParent : Component<IParent>
{
    private Parent _data;

    public Task OnAddAsync(Parent initialData, CancellationToken ct = default)
    {
        _data = initialData;
        return Task.CompletedTask;
    }

    public Task OnRemoveAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task<Parent> GetAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_data);
    }

    public Task SetAsync(Parent data, CancellationToken ct = default)
    {
        _data = data;
        RaiseUpdated(data);
        return Task.CompletedTask;
    }
}
