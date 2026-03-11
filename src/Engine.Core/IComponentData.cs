namespace Engine.Core;

public interface IComponentData<T>
{
    Task InitDataAsync(T data, CancellationToken ct = default);

    Task<T> GetDataAsync(CancellationToken ct = default);

    Task SetDataAsync(T data, CancellationToken ct = default);
}
