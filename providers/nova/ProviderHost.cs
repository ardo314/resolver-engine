using System.Text.Json;
using Engine.Core;
using NATS.Client.Core;

namespace Providers.Nova;

/// <summary>
/// Hosts component providers. Registers components with the engine backend,
/// listens for start/stop lifecycle events, and manages provider instances.
/// </summary>
public class ProviderHost
{
    private readonly INatsConnection _nc;
    private readonly Dictionary<string, Func<ComponentProvider>> _providerFactories = new();
    private readonly Dictionary<string, Component> _providerComponents = new();
    private readonly Dictionary<string, ComponentProvider> _activeProviders = new();

    public ProviderHost(INatsConnection nc) => _nc = nc;

    public void Register<T>(Func<T> factory) where T : ComponentProvider
    {
        var instance = factory();
        var component = instance.Component;
        _providerFactories[component.Id] = factory;
        _providerComponents[component.Id] = component;
    }

    public async Task Listen(CancellationToken ct = default)
    {
        await RegisterComponents(ct);

        var startTask = HandleStartWorker(ct);
        var stopTask = HandleStopWorker(ct);

        await Task.WhenAll(startTask, stopTask);
    }

    private async Task RegisterComponents(CancellationToken ct)
    {
        foreach (var (componentId, component) in _providerComponents)
        {
            var methodNames = component.MethodNames;
            var schema = new { methods = new Dictionary<string, object>() };
            var payload = JsonSerializer.Serialize(new { componentId, methodNames, schema });
            var reply = await _nc.RequestAsync<string, string>(
                Subjects.RegisterComponent, payload, cancellationToken: ct);
            using var doc = JsonDocument.Parse(reply.Data!);
            if (doc.RootElement.TryGetProperty("error", out var err))
                throw new InvalidOperationException(
                    $"Failed to register component {componentId}: {err.GetString()}");
        }
    }

    private async Task HandleStartWorker(CancellationToken ct)
    {
        await foreach (var msg in _nc.SubscribeAsync<string>(Subjects.StartWorker, cancellationToken: ct))
        {
            try
            {
                using var doc = JsonDocument.Parse(msg.Data!);
                var root = doc.RootElement;
                var entityId = new EntityId(root.GetProperty("entityId").GetString()!);
                var componentId = root.GetProperty("componentId").GetString()!;

                if (!_providerFactories.TryGetValue(componentId, out var factory))
                    continue;

                var key = $"{entityId.Value}:{componentId}";
                var provider = factory();
                await provider.Start(_nc, entityId, ct);
                _activeProviders[key] = provider;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Failed to start provider: {e.Message}");
            }
        }
    }

    private async Task HandleStopWorker(CancellationToken ct)
    {
        await foreach (var msg in _nc.SubscribeAsync<string>(Subjects.StopWorker, cancellationToken: ct))
        {
            try
            {
                using var doc = JsonDocument.Parse(msg.Data!);
                var root = doc.RootElement;
                var entityId = new EntityId(root.GetProperty("entityId").GetString()!);
                var componentId = root.GetProperty("componentId").GetString()!;

                if (!_providerFactories.ContainsKey(componentId))
                    continue;

                var key = $"{entityId.Value}:{componentId}";
                if (_activeProviders.TryGetValue(key, out var provider))
                {
                    await provider.Stop();
                    _activeProviders.Remove(key);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Failed to stop provider: {e.Message}");
            }
        }
    }
}
