using Engine.Core;
using Engine.Worker;
using InMemory;

namespace InMemory.Workers;

public partial class InMemoryParentWorker : ComponentWorker<InMemoryParent>
{
    private EntityId? _parentId;

    public Task<EntityId> GetDataAsync(CancellationToken ct = default)
    {
        if (_parentId is null)
            throw new InvalidOperationException("Parent ID has not been set.");

        return Task.FromResult(_parentId.Value);
    }

    public Task SetDataAsync(EntityId data, CancellationToken ct = default)
    {
        _parentId = data;
        return Task.CompletedTask;
    }
}
