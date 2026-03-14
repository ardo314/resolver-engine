using System.Reflection;
using Engine.Core;
using Engine.Module;
using NATS.Client.Core;

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// ── Load module assemblies and discover BehaviourWorkers ────────────────

var modulesDir = Path.Combine(AppContext.BaseDirectory, "modules");

if (!Directory.Exists(modulesDir))
{
    Console.WriteLine($"No modules directory found at: {modulesDir}");
    return;
}

// Registry: behaviour name → concrete worker Type
var workerTypes = new Dictionary<string, Type>();

foreach (var dllPath in Directory.EnumerateFiles(modulesDir, "*.dll"))
{
    Assembly assembly;
    try
    {
        assembly = Assembly.LoadFrom(dllPath);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to load assembly {dllPath}: {ex.Message}");
        continue;
    }

    foreach (var type in assembly.GetExportedTypes())
    {
        if (type.IsAbstract || type.IsInterface)
            continue;

        if (!IsBehaviourWorker(type))
            continue;

        var behaviourType = GetBehaviourTypeArgument(type);
        if (behaviourType is null)
            continue;

        var behaviourName = behaviourType.Name;
        workerTypes[behaviourName] = type;
        Console.WriteLine(
            $"Registered BehaviourWorker: {type.FullName} (behaviour: {behaviourName})"
        );
    }
}

Console.WriteLine($"Registered {workerTypes.Count} behaviour worker type(s).");

// ── Connect to NATS and subscribe to worker lifecycle subjects ──────────

await using var nats = new NatsConnection();
await nats.ConnectAsync();

// Tracks live worker instances keyed by (EntityId, behaviourName)
var workers = new Dictionary<(EntityId, string), object>();

// The non-generic base method for calling OnAddedAsync / OnRemovedAsync via reflection
var onAddedMethod = typeof(BehaviourWorker<>).GetMethod(
    nameof(BehaviourWorker<IBehaviour>.OnAddedAsync)
)!;
var onRemovedMethod = typeof(BehaviourWorker<>).GetMethod(
    nameof(BehaviourWorker<IBehaviour>.OnRemovedAsync)
)!;
var entityIdProperty = typeof(BehaviourWorker<>).GetProperty(
    nameof(BehaviourWorker<IBehaviour>.EntityId)
)!;

var subscriptions = new List<IAsyncDisposable>();

foreach (var (behaviourName, workerType) in workerTypes)
{
    // Subscribe to worker.create.<behaviourName>
    var createSub = await nats.SubscribeCoreAsync<string>(
        $"worker.create.{behaviourName}",
        cancellationToken: cts.Token
    );
    subscriptions.Add(createSub);

    _ = Task.Run(
        async () =>
        {
            await foreach (var msg in createSub.Msgs.ReadAllAsync(cts.Token))
            {
                try
                {
                    if (!Guid.TryParse(msg.Data, out var guid))
                    {
                        await msg.ReplyAsync("error: invalid EntityId format");
                        continue;
                    }

                    var entityId = new EntityId(guid);
                    var key = (entityId, behaviourName);

                    if (workers.ContainsKey(key))
                    {
                        await msg.ReplyAsync("error: worker already exists for this entity");
                        continue;
                    }

                    var instance = Activator.CreateInstance(workerType);
                    if (instance is null)
                    {
                        await msg.ReplyAsync("error: failed to create worker instance");
                        continue;
                    }

                    // Set EntityId property on the concrete BehaviourWorker<T>
                    var concreteBaseType = GetBehaviourWorkerBaseType(workerType)!;
                    concreteBaseType
                        .GetProperty(nameof(BehaviourWorker<IBehaviour>.EntityId))!
                        .SetValue(instance, entityId);

                    // Call OnAddedAsync
                    var concreteOnAdded = concreteBaseType.GetMethod(
                        nameof(BehaviourWorker<IBehaviour>.OnAddedAsync)
                    )!;
                    var task = (Task)concreteOnAdded.Invoke(instance, [CancellationToken.None])!;
                    await task;

                    workers[key] = instance;
                    Console.WriteLine(
                        $"Created worker {workerType.FullName} for entity {entityId}"
                    );
                    await msg.ReplyAsync("ok");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating worker: {ex.Message}");
                    await msg.ReplyAsync($"error: {ex.Message}");
                }
            }
        },
        cts.Token
    );

    // Subscribe to worker.remove.<behaviourName>
    var removeSub = await nats.SubscribeCoreAsync<string>(
        $"worker.remove.{behaviourName}",
        cancellationToken: cts.Token
    );
    subscriptions.Add(removeSub);

    _ = Task.Run(
        async () =>
        {
            await foreach (var msg in removeSub.Msgs.ReadAllAsync(cts.Token))
            {
                try
                {
                    if (!Guid.TryParse(msg.Data, out var guid))
                    {
                        await msg.ReplyAsync("error: invalid EntityId format");
                        continue;
                    }

                    var entityId = new EntityId(guid);
                    var key = (entityId, behaviourName);

                    if (!workers.TryGetValue(key, out var instance))
                    {
                        await msg.ReplyAsync("error: no worker found for this entity");
                        continue;
                    }

                    // Call OnRemovedAsync
                    var concreteBaseType = GetBehaviourWorkerBaseType(workerType)!;
                    var concreteOnRemoved = concreteBaseType.GetMethod(
                        nameof(BehaviourWorker<IBehaviour>.OnRemovedAsync)
                    )!;
                    var task = (Task)concreteOnRemoved.Invoke(instance, [CancellationToken.None])!;
                    await task;

                    workers.Remove(key);
                    Console.WriteLine(
                        $"Removed worker {workerType.FullName} for entity {entityId}"
                    );
                    await msg.ReplyAsync("ok");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error removing worker: {ex.Message}");
                    await msg.ReplyAsync($"error: {ex.Message}");
                }
            }
        },
        cts.Token
    );
}

Console.WriteLine("Engine.ModuleRuntime running – press Ctrl+C to stop.");

try
{
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
    // Graceful shutdown
}

foreach (var sub in subscriptions)
{
    await sub.DisposeAsync();
}

// ── Helpers ─────────────────────────────────────────────────────────────

/// <summary>
/// Checks whether a type derives from <see cref="BehaviourWorker{T}"/> for some T.
/// </summary>
static bool IsBehaviourWorker(Type type)
{
    var current = type.BaseType;
    while (current is not null)
    {
        if (
            current.IsGenericType
            && current.GetGenericTypeDefinition() == typeof(BehaviourWorker<>)
        )
            return true;

        current = current.BaseType;
    }

    return false;
}

/// <summary>
/// Extracts the <c>T</c> from the <see cref="BehaviourWorker{T}"/> base class.
/// </summary>
static Type? GetBehaviourTypeArgument(Type workerType)
{
    var current = workerType.BaseType;
    while (current is not null)
    {
        if (
            current.IsGenericType
            && current.GetGenericTypeDefinition() == typeof(BehaviourWorker<>)
        )
            return current.GetGenericArguments()[0];

        current = current.BaseType;
    }

    return null;
}

/// <summary>
/// Returns the closed <c>BehaviourWorker&lt;T&gt;</c> base type for reflection calls.
/// </summary>
static Type? GetBehaviourWorkerBaseType(Type workerType)
{
    var current = workerType.BaseType;
    while (current is not null)
    {
        if (
            current.IsGenericType
            && current.GetGenericTypeDefinition() == typeof(BehaviourWorker<>)
        )
            return current;

        current = current.BaseType;
    }

    return null;
}
