using System.Text.Json;

namespace Engine.Core;

public sealed record MethodDefinition(
    Type? InputType = null,
    Type? OutputType = null
);

public sealed record Method(string Name, MethodDefinition Definition)
{
    public static Method Define(string name) =>
        new(name, new MethodDefinition());

    public static Method Define<TOutput>(string name) =>
        new(name, new MethodDefinition(OutputType: typeof(TOutput)));

    public static Method DefineWithInput<TInput>(string name) =>
        new(name, new MethodDefinition(InputType: typeof(TInput)));

    public static Method Define<TInput, TOutput>(string name) =>
        new(name, new MethodDefinition(InputType: typeof(TInput), OutputType: typeof(TOutput)));
}

public sealed record MethodSchema(
    JsonElement? Input = null,
    JsonElement? Output = null
);
