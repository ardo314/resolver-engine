using System.Text.Json;
using Engine.Core;
using Modules.Nova;

namespace Providers.Nova;

public class NameProvider : ComponentProvider
{
    private string _name = "";

    public NameProvider() : base(NovaComponents.Name) { }

    protected override Task<JsonElement?> HandleMethod(string methodName, JsonElement? input)
    {
        return methodName switch
        {
            "getName" => Task.FromResult<JsonElement?>(
                JsonSerializer.SerializeToElement(_name)),
            "setName" => SetName(input),
            _ => throw new InvalidOperationException($"Unknown method: {methodName}")
        };
    }

    private Task<JsonElement?> SetName(JsonElement? input)
    {
        _name = input?.GetString() ?? "";
        return Task.FromResult<JsonElement?>(null);
    }
}
