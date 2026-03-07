namespace Engine.Core;

/// <summary>
/// Defines a contract for reading and writing data of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of data this resolver manages.</typeparam>
public interface IDataResolver<T>
{
    /// <summary>
    /// Gets the current value.
    /// </summary>
    T Get();

    /// <summary>
    /// Sets the value.
    /// </summary>
    void Set(T value);
}
