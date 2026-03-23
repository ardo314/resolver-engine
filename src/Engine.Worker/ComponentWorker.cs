using Engine.Client;
using Engine.Core;

namespace Engine.Worker;

public abstract class ComponentWorker<T>
    where T : struct, IComponent
{
    public EntityId EntityId { get; private set; }

    public virtual Task OnAddedAsync(CancellationToken ct = default) => Task.CompletedTask;

    public virtual Task OnRemovedAsync(CancellationToken ct = default) => Task.CompletedTask;
}
