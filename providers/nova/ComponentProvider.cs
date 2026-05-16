using System.Text.Json;
using Engine.Core;
using NATS.Client.Core;

namespace Providers.Nova;

/// <summary>
/// Base class for component providers. Providers implement the runtime behaviour
/// for a component — one instance per component on an entity.
/// </summary>
public abstract class ComponentProvider
{
    protected INatsConnection Nc { get; private set; } = null!;
    protected EntityId EntityId { get; private set; }

    private readonly List<IAsyncDisposable> _subscriptions = new();

    public Component Component { get; }

    protected ComponentProvider(Component component)
    {
        Component = component;
    }

    /// <summary>Called after the provider is fully wired (all subscriptions active).</summary>
    protected virtual Task OnAdded() => Task.CompletedTask;

    /// <summary>Called before the provider is torn down.</summary>
    protected virtual Task OnRemoved() => Task.CompletedTask;

    /// <summary>
    /// Dispatch a method call. Implementations must handle all methods defined
    /// on the component and return the result as a JsonElement (or null for void methods).
    /// </summary>
    protected abstract Task<JsonElement?> HandleMethod(string methodName, JsonElement? input);

    public async Task Start(INatsConnection nc, EntityId entityId, CancellationToken ct = default)
    {
        Nc = nc;
        EntityId = entityId;

        foreach (var method in Component.Methods)
        {
            var subject = WorkerSubjects.CallMethod(Component.Id, entityId.Value, method.Name);
            await SubscribeMethod(subject, method.Name, ct);
        }

        await OnAdded();
    }

    public async Task Stop()
    {
        await OnRemoved();
        foreach (var sub in _subscriptions)
            await sub.DisposeAsync();
        _subscriptions.Clear();
    }

    private async Task SubscribeMethod(string subject, string methodName, CancellationToken ct)
    {
        var sub = await Nc.SubscribeCoreAsync<string>(subject, cancellationToken: ct);
        _subscriptions.Add(sub);

        _ = Task.Run(async () =>
        {
            await foreach (var msg in sub.Msgs.ReadAllAsync(ct))
            {
                try
                {
                    JsonElement? input = null;
                    if (!string.IsNullOrEmpty(msg.Data))
                    {
                        using var doc = JsonDocument.Parse(msg.Data);
                        if (doc.RootElement.TryGetProperty("input", out var inp))
                            input = inp.Clone();
                    }

                    var result = await HandleMethod(methodName, input);
                    var response = result.HasValue
                        ? JsonSerializer.Serialize(new { result = result.Value })
                        : JsonSerializer.Serialize(new { result = (object?)null });
                    await msg.ReplyAsync(response, cancellationToken: ct);
                }
                catch (Exception e)
                {
                    await msg.ReplyAsync(
                        JsonSerializer.Serialize(new { error = e.Message }),
                        cancellationToken: ct);
                }
            }
        }, ct);
    }
}
