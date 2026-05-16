using System.Text.Json;
using Engine.Core;
using Modules.Nova;

namespace Providers.Nova;

public class ParentProvider : ComponentProvider
{
    private string _parentId = "";

    public ParentProvider() : base(NovaComponents.Parent) { }

    protected override Task<JsonElement?> HandleMethod(string methodName, JsonElement? input)
    {
        return methodName switch
        {
            "getParent" => Task.FromResult<JsonElement?>(
                JsonSerializer.SerializeToElement(_parentId)),
            "setParent" => SetParent(input),
            _ => throw new InvalidOperationException($"Unknown method: {methodName}")
        };
    }

    private Task<JsonElement?> SetParent(JsonElement? input)
    {
        _parentId = input?.GetString() ?? "";
        return Task.FromResult<JsonElement?>(null);
    }
}
