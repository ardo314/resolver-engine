namespace Engine.Core;

public sealed record Query(IReadOnlyList<Method> Methods)
{
    public static Query Define(params Method[] methods) => new(methods);

    /// <summary>
    /// Allow using a Component as a Query (duck-typing).
    /// </summary>
    public static implicit operator Query(Component component) =>
        new(component.Methods);
}
