using System.ComponentModel;
using Engine.Core;
using Engine.Hierarchy;

namespace Modules.InMemoryParent;

public partial class InMemoryParent : Component, IParent
{
    public Task InitDataAsync(EntityId data, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<EntityId> GetDataAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task SetDataAsync(EntityId data, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
