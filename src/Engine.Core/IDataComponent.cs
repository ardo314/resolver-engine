namespace Engine.Core;

public interface IDataComponent<T> : IComponent
{
    Task<T> GetDataAsync(CancellationToken ct = default);

    Task SetDataAsync(T data, CancellationToken ct = default);
}
