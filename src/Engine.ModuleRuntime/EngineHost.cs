using Engine.Core;
using NATS.Client.Core;

namespace Engine.ModuleRuntime;

/// <summary>
/// Hosts the engine runtime — loads extensions, connects to NATS, and dispatches messages.
/// </summary>
public sealed class EngineHost : IAsyncDisposable
{
    private readonly NatsConnection _connection;
    private readonly ExtensionRegistrar _registrar = new();
    private readonly List<IExtension> _extensions = [];
    private readonly List<Plugin> _plugins = [];

    public EngineHost(NatsOpts? opts = null)
    {
        _connection = new NatsConnection(opts ?? NatsOpts.Default);
    }

    /// <summary>
    /// The collected extension registrations.
    /// </summary>
    public ExtensionRegistrar Registrar => _registrar;

    /// <summary>
    /// The underlying NATS connection.
    /// </summary>
    public INatsConnection Connection => _connection;

    /// <summary>
    /// Loads extensions from the given directory, registers them, connects to NATS,
    /// and starts listening.
    /// </summary>
    public async Task StartAsync(
        string extensionsPath = "/app/extensions",
        CancellationToken ct = default
    )
    {
        // 1. Discover and load extensions
        var extensions = ExtensionLoader.LoadFrom(extensionsPath);
        _extensions.AddRange(extensions);

        // 2. Let each extension register its types
        foreach (var extension in _extensions)
        {
            extension.Register(_registrar);
        }

        // 3. Connect to NATS
        await _connection.ConnectAsync();

        Console.WriteLine(
            $"Engine runtime started — {_extensions.Count} extension(s), "
                + $"{_registrar.ComponentTypes.Count} component(s), "
                + $"{_registrar.BehaviourTypes.Count} behaviour(s), "
                + $"{_registrar.PluginTypes.Count} plugin(s)"
        );

        // 4. Instantiate and start plugins
        // TODO: Create a proper IWorld implementation
        foreach (var pluginType in _registrar.PluginTypes)
        {
            if (Activator.CreateInstance(pluginType) is Plugin plugin)
            {
                // plugin.Initialize(world); // Set when IWorld implementation exists
                _plugins.Add(plugin);
                await plugin.OnStartAsync(ct);
            }
        }

        // TODO: Set up NATS subscriptions based on registered behaviours
    }

    /// <summary>
    /// Runs the host until cancellation is requested.
    /// </summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        await StartAsync(ct: ct);

        // Block until cancelled
        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown — stop plugins in reverse order
            for (var i = _plugins.Count - 1; i >= 0; i--)
            {
                await _plugins[i].OnStopAsync(CancellationToken.None);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
