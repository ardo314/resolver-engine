namespace Engine.Core;

/// <summary>
/// Base class for the generic <see cref="HasAttribute{T}"/> attribute.
/// Allows runtime reflection without knowing the type argument.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public abstract class HasAttribute : Attribute
{
    /// <summary>
    /// The component interface type this struct provides.
    /// </summary>
    public abstract Type ComponentType { get; }
}

/// <summary>
/// Marks a component marker struct as providing the given <typeparamref name="T"/> component interface.
/// Used by the source generator to determine which interfaces a worker must implement,
/// and by the client-side <c>AddComponentAsync</c> to discover component names at runtime.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class HasAttribute<T> : HasAttribute
    where T : IComponent
{
    public override Type ComponentType => typeof(T);
}
