namespace Engine.Generators;

/// <summary>
/// Deterministic NATS subject derivation from interface and method names.
/// </summary>
internal static class SubjectNaming
{
    /// <summary>
    /// Derives the NATS subject for a behaviour method.
    /// Convention: <c>behaviour.{TypeName}.{MethodName}</c>
    /// The leading 'I' on interface names is stripped.
    /// </summary>
    public static string ForBehaviourMethod(string interfaceName, string methodName)
    {
        var typeName = StripLeadingI(interfaceName);
        var method = StripAsyncSuffix(methodName);
        return $"behaviour.{typeName}.{method}";
    }

    private static string StripLeadingI(string name)
    {
        if (name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]))
            return name.Substring(1);
        return name;
    }

    private static string StripAsyncSuffix(string name)
    {
        if (name.EndsWith("Async"))
            return name.Substring(0, name.Length - 5);
        return name;
    }
}
