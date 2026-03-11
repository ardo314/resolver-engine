namespace Engine.Core;

public interface IComponentLifecycle
{
    Task OnAddAsync(CancellationToken ct = default);

    Task OnRemoveAsync(CancellationToken ct = default);
}
