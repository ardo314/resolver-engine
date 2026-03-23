namespace Engine.Client;

/// <summary>
/// Base class for the generic <see cref="HasBehaviourAttribute{T}"/> attribute.
/// Allows runtime reflection without knowing the type argument.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public abstract class HasBehaviourAttribute : Attribute
{
    /// <summary>
    /// The behaviour interface type this struct provides.
    /// </summary>
    public abstract Type ComponentType { get; }
}

/// <summary>
/// Marks a component marker struct as providing the given <typeparamref name="T"/> behaviour interface.
/// Used by the source generator to determine which interfaces a worker must implement,
/// and by the client-side <c>AddComponentAsync</c> to discover component names at runtime.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class HasBehaviourAttribute<T> : HasBehaviourAttribute
    where T : IBehaviour
{
    public override Type ComponentType => typeof(T);
}
