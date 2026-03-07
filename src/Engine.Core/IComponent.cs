namespace Engine.Core;

/// <summary>
/// Marker interface for all components in the entity-component system.
/// </summary>
public interface IComponent { }

/// <summary>
/// A typed data component that supports reading and writing a value of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of data this component manages.</typeparam>
public interface IComponent<T> : IComponent
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
