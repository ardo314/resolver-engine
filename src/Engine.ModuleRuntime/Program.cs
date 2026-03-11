using Engine.ModuleRuntime;

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await using var host = new EngineHost();
await host.RunAsync(cts.Token);
