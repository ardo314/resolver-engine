using Engine.Core;

namespace Engine.Module;

public abstract class BehaviourWorker<T>
    where T : IBehaviour
{
    public EntityId EntityId { get; private set; }

    public virtual Task OnAddedAsync(CancellationToken ct = default) => Task.CompletedTask;

    public virtual Task OnRemovedAsync(CancellationToken ct = default) => Task.CompletedTask;
}
