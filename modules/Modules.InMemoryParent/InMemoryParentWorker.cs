using Engine.Core;
using Engine.Module;

namespace Modules.InMemoryParent;

public partial class InMemoryParentWorker : BehaviourWorker<IParent>
{
    private EntityId? _parentId;

    public async Task<EntityId> GetDataAsync(CancellationToken ct = default)
    {
        if (_parentId is null)
            throw new InvalidOperationException("Parent ID has not been set.");

        return _parentId.Value;
    }

    public async Task SetDataAsync(EntityId data, CancellationToken ct = default)
    {
        _parentId = data;
    }
}
