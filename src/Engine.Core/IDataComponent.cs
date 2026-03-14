using System.ComponentModel;

namespace Engine.Core;

public interface IDataComponent<T> : IComponent
{
    Task InitDataAsync(T data, CancellationToken ct = default);

    Task<T> GetDataAsync(CancellationToken ct = default);

    Task SetDataAsync(T data, CancellationToken ct = default);
}
