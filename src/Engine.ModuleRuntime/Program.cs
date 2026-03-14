using System.Reflection;
using Engine.Core;
using Engine.Module;

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

var workerInstances = new List<object>();

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
        var instance = Activator.CreateInstance(type);

        if (instance is null)
        {
            Console.WriteLine($"Failed to create instance of {type.FullName}");
            continue;
        }

        workerInstances.Add(instance);
        Console.WriteLine(
            $"Loaded BehaviourWorker: {type.FullName} (behaviour: {behaviourType?.Name})"
        );
    }
}

Console.WriteLine($"Discovered {workerInstances.Count} behaviour worker(s).");

try
{
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
    // Graceful shutdown
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
