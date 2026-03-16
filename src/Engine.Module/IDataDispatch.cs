namespace Engine.Module;

/// <summary>
/// Implemented by generated partial classes to dispatch component method calls
/// from the module runtime without reflection. The module runtime receives raw
/// NATS messages and delegates to this interface.
/// </summary>
public interface IDataDispatch
{
    /// <summary>
    /// Dispatches a component method call identified by <paramref name="componentName"/> and <paramref name="methodName"/>.
    /// </summary>
    /// <param name="componentName">The name of the component interface (e.g. "IPose").</param>
    /// <param name="methodName">The name of the component method to invoke.</param>
    /// <param name="payload">MessagePack-serialized parameter data (empty if the method has no parameter).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>MessagePack-serialized return value, or empty if the method returns <see cref="Task"/>.</returns>
    Task<ReadOnlyMemory<byte>> DispatchAsync(
        string componentName,
        string methodName,
        ReadOnlyMemory<byte> payload,
        CancellationToken ct
    );
}
