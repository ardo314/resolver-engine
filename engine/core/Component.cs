namespace Engine.Core;

public sealed record Component
{
    public string Id { get; }
    public IReadOnlyList<Method> Methods { get; }

    private Component(string id, IReadOnlyList<Method> methods)
    {
        Id = id;
        Methods = methods;
    }

    public static Component Define(string id, params Method[] methods)
    {
        ValidateNoDuplicates(id, methods);
        return new Component(id, methods);
    }

    public IReadOnlyList<string> MethodNames =>
        Methods.Select(m => m.Name).ToList();

    private static void ValidateNoDuplicates(string id, Method[] methods)
    {
        var seen = new HashSet<string>();
        foreach (var method in methods)
        {
            if (!seen.Add(method.Name))
                throw new ArgumentException(
                    $"Component \"{id}\": duplicate method name \"{method.Name}\"");
        }
    }
}
