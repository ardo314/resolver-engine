using System.ComponentModel;
using Engine.Core;

namespace Engine.Module;

public abstract class ComponentWorker<T>
    where T : IComponent
{
    public virtual async Task OnAddedAsync(T component, CancellationToken ct = default)
    {
        // Default implementation for when a component is added to an entity
    }

    public virtual async Task OnRemovedAsync(T component, CancellationToken ct = default)
    {
        // Default implementation for when a component is removed from an entity
    }
}
