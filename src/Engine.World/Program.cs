using Engine.Runtime;
using Engine.World;
using NATS.Client.Core;

var opts = NatsOpts.Default with { SerializerRegistry = MessagePackNatsSerializerRegistry.Default };
await using var connection = new NatsConnection(opts);

Console.WriteLine("World server starting... Press Ctrl+C to stop.");

var world = new WorldService();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    await world.StartAsync(connection, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("World server stopped.");
}
