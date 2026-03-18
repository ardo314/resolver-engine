namespace Engine.Client;

/// <summary>
/// Convenience base for behaviours that hold typed data with async get/set methods.
/// </summary>
public interface IDataBehaviour<T> : IBehaviour
{
    Task<T> GetDataAsync(CancellationToken ct = default);

    Task SetDataAsync(T data, CancellationToken ct = default);
}
