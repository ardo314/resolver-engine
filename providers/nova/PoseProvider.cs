using System.Text.Json;
using Engine.Core;
using Modules.Nova;

namespace Providers.Nova;

public class PoseProvider : ComponentProvider
{
    private double[] _pose = [0, 0, 0, 0, 0, 0];

    public PoseProvider() : base(NovaComponents.Pose) { }

    protected override Task<JsonElement?> HandleMethod(string methodName, JsonElement? input)
    {
        return methodName switch
        {
            "getPose" => Task.FromResult<JsonElement?>(
                JsonSerializer.SerializeToElement(_pose)),
            "setPose" => SetPose(input),
            _ => throw new InvalidOperationException($"Unknown method: {methodName}")
        };
    }

    private Task<JsonElement?> SetPose(JsonElement? input)
    {
        if (input.HasValue)
        {
            var values = input.Value.EnumerateArray().Select(e => e.GetDouble()).ToArray();
            if (values.Length == 6)
                _pose = values;
        }
        return Task.FromResult<JsonElement?>(null);
    }
}
