using Engine.Core;

namespace Engine.Module;

public abstract class ComponentWorker<T>
    where T : struct
{
    public EntityId EntityId { get; private set; }

    public virtual Task OnAddedAsync(CancellationToken ct = default) => Task.CompletedTask;

    public virtual Task OnRemovedAsync(CancellationToken ct = default) => Task.CompletedTask;
}
