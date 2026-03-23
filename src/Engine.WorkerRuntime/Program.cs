using System.Reflection;
using Engine.Client;
using Engine.Core;
using Engine.Worker;
using MessagePack;
using NATS.Client.Core;

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// ── Load module assemblies and discover ComponentWorkers ────────────────

var modulesDir = Path.Combine(AppContext.BaseDirectory, "modules");

if (!Directory.Exists(modulesDir))
{
    Console.WriteLine($"No modules directory found at: {modulesDir}");
    return;
}

// Registry: marker struct name → concrete worker Type
var workerTypes = new Dictionary<string, Type>();

// Mapping: component interface name → marker struct name (for dispatch routing)
var componentToStructName = new Dictionary<string, string>();

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

        if (!IsComponentWorker(type))
            continue;

        var markerStruct = GetMarkerStructArgument(type);
        if (markerStruct is null)
            continue;

        var structName = markerStruct.Name;
        workerTypes[structName] = type;

        // Read [HasBehaviour<>] attributes from the marker struct to discover component interfaces
        var hasAttributes = markerStruct
            .GetCustomAttributes(inherit: false)
            .Where(a =>
                a.GetType().IsGenericType
                && a.GetType().GetGenericTypeDefinition() == typeof(HasBehaviourAttribute<>)
            )
            .ToList();

        foreach (var attr in hasAttributes)
        {
            var componentType = ((HasBehaviourAttribute)attr).ComponentType;
            componentToStructName[componentType.Name] = structName;
            Console.WriteLine($"  Component interface: {componentType.Name} → {structName}");
        }

        Console.WriteLine($"Registered ComponentWorker: {type.FullName} (struct: {structName})");
    }
}

Console.WriteLine($"Registered {workerTypes.Count} component worker type(s).");

// ── Connect to NATS and subscribe to worker lifecycle subjects ──────────

await using var nats = new NatsConnection();
await nats.ConnectAsync();

// Tracks live worker instances keyed by (EntityId, structName)
var workers = new Dictionary<(EntityId, string), object>();

// Tracks (EntityId, componentInterfaceName) → worker instance for dispatch routing
var componentWorkers = new Dictionary<(EntityId, string), object>();

var subscriptions = new List<IAsyncDisposable>();

foreach (var (structName, workerType) in workerTypes)
{
    // Collect the component interface names this struct provides
    var markerStruct = GetMarkerStructArgument(workerType)!;
    var interfaceNames = markerStruct
        .GetCustomAttributes(inherit: false)
        .Where(a =>
            a.GetType().IsGenericType
            && a.GetType().GetGenericTypeDefinition() == typeof(HasBehaviourAttribute<>)
        )
        .Select(a => ((HasBehaviourAttribute)a).ComponentType.Name)
        .ToList();

    // Subscribe to worker.create.<structName>
    var createSub = await nats.SubscribeCoreAsync<string>(
        $"worker.create.{structName}",
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
                    var key = (entityId, structName);

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

                    // Set EntityId property on the concrete ComponentWorker<T>
                    var concreteBaseType = GetComponentWorkerBaseType(workerType)!;
                    concreteBaseType.GetProperty("EntityId")!.SetValue(instance, entityId);

                    // Call OnAddedAsync
                    var concreteOnAdded = concreteBaseType.GetMethod("OnAddedAsync")!;
                    var task = (Task)concreteOnAdded.Invoke(instance, [CancellationToken.None])!;
                    await task;

                    workers[key] = instance;

                    // Register for all component interfaces this struct provides
                    foreach (var ifaceName in interfaceNames)
                    {
                        componentWorkers[(entityId, ifaceName)] = instance;
                    }

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

    // Subscribe to worker.remove.<structName>
    var removeSub = await nats.SubscribeCoreAsync<string>(
        $"worker.remove.{structName}",
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
                    var key = (entityId, structName);

                    if (!workers.TryGetValue(key, out var instance))
                    {
                        await msg.ReplyAsync("error: no worker found for this entity");
                        continue;
                    }

                    // Call OnRemovedAsync
                    var concreteBaseType = GetComponentWorkerBaseType(workerType)!;
                    var concreteOnRemoved = concreteBaseType.GetMethod("OnRemovedAsync")!;
                    var task = (Task)concreteOnRemoved.Invoke(instance, [CancellationToken.None])!;
                    await task;

                    workers.Remove(key);

                    // Unregister all component interfaces
                    foreach (var ifaceName in interfaceNames)
                    {
                        componentWorkers.Remove((entityId, ifaceName));
                    }

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

// ── Subscribe to component method dispatch subjects ─────────────────────

// Build the set of unique component interface names to subscribe to
var allComponentNames = componentToStructName.Keys.ToHashSet();

foreach (var componentName in allComponentNames)
{
    // Subscribe to component.<componentName>.> (wildcard for all methods)
    var dispatchSub = await nats.SubscribeCoreAsync<byte[]>(
        $"component.{componentName}.*",
        cancellationToken: cts.Token
    );
    subscriptions.Add(dispatchSub);

    _ = Task.Run(
        async () =>
        {
            await foreach (var msg in dispatchSub.Msgs.ReadAllAsync(cts.Token))
            {
                try
                {
                    // Extract method name from subject: component.<name>.<method>
                    var subject = msg.Subject;
                    var lastDot = subject.LastIndexOf('.');
                    if (lastDot < 0)
                    {
                        await msg.ReplyAsync("error: invalid subject format");
                        continue;
                    }
                    var methodName = subject.Substring(lastDot + 1);

                    // EntityId can come from a header (when payload is serialized param data)
                    // or from the payload itself (when there is no param — payload is a Guid string).
                    EntityId entityId;
                    ReadOnlyMemory<byte> dispatchPayload;

                    if (
                        msg.Headers is not null
                        && msg.Headers.TryGetValue("EntityId", out var entityIdValues)
                        && entityIdValues.Count > 0
                        && Guid.TryParse(entityIdValues[0], out var headerGuid)
                    )
                    {
                        entityId = new EntityId(headerGuid);
                        dispatchPayload = msg.Data ?? ReadOnlyMemory<byte>.Empty;
                    }
                    else
                    {
                        // No header — payload is a UTF-8 Guid string (no-param methods).
                        var payloadBytes = msg.Data ?? Array.Empty<byte>();
                        var payloadStr = System.Text.Encoding.UTF8.GetString(payloadBytes);
                        if (
                            !Guid.TryParse(
                                payloadStr.AsSpan().Trim((char)0).Trim('"'),
                                out var payloadGuid
                            )
                        )
                        {
                            await msg.ReplyAsync("error: invalid EntityId format");
                            continue;
                        }
                        entityId = new EntityId(payloadGuid);
                        dispatchPayload = ReadOnlyMemory<byte>.Empty;
                    }

                    var key = (entityId, componentName);
                    if (!componentWorkers.TryGetValue(key, out var instance))
                    {
                        await msg.ReplyAsync("error: no worker found for this entity");
                        continue;
                    }

                    if (instance is not IDataDispatch dispatch)
                    {
                        await msg.ReplyAsync("error: worker does not support data dispatch");
                        continue;
                    }

                    var result = await dispatch.DispatchAsync(
                        componentName,
                        methodName,
                        dispatchPayload,
                        cts.Token
                    );

                    if (result.Length > 0)
                    {
                        await msg.ReplyAsync(result.ToArray());
                    }
                    else
                    {
                        await msg.ReplyAsync(System.Text.Encoding.UTF8.GetBytes("ok"));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error dispatching component method: {ex.Message}");
                    await msg.ReplyAsync(
                        System.Text.Encoding.UTF8.GetBytes($"error: {ex.Message}")
                    );
                }
            }
        },
        cts.Token
    );
}

Console.WriteLine("Engine.WorkerRuntime running – press Ctrl+C to stop.");

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
/// Checks whether a type derives from <see cref="ComponentWorker{T}"/> for some T.
/// </summary>
static bool IsComponentWorker(Type type)
{
    var current = type.BaseType;
    while (current is not null)
    {
        if (
            current.IsGenericType
            && current.GetGenericTypeDefinition() == typeof(ComponentWorker<>)
        )
            return true;

        current = current.BaseType;
    }

    return false;
}

/// <summary>
/// Extracts the marker struct <c>T</c> from the <see cref="ComponentWorker{T}"/> base class.
/// </summary>
static Type? GetMarkerStructArgument(Type workerType)
{
    var current = workerType.BaseType;
    while (current is not null)
    {
        if (
            current.IsGenericType
            && current.GetGenericTypeDefinition() == typeof(ComponentWorker<>)
        )
            return current.GetGenericArguments()[0];

        current = current.BaseType;
    }

    return null;
}

/// <summary>
/// Returns the closed <c>ComponentWorker&lt;T&gt;</c> base type for reflection calls.
/// </summary>
static Type? GetComponentWorkerBaseType(Type workerType)
{
    var current = workerType.BaseType;
    while (current is not null)
    {
        if (
            current.IsGenericType
            && current.GetGenericTypeDefinition() == typeof(ComponentWorker<>)
        )
            return current;

        current = current.BaseType;
    }

    return null;
}
