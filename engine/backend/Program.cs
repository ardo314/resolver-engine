using Engine.Backend;
using NATS.Client.Core;

var url = Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://localhost:4222";
var user = Environment.GetEnvironmentVariable("NATS_USER");
var pass = Environment.GetEnvironmentVariable("NATS_PASS");

var opts = new NatsOpts
{
    Url = url,
    AuthOpts = (user is not null && pass is not null)
        ? NatsAuthOpts.Default with { Username = user, Password = pass }
        : NatsAuthOpts.Default,
};

await using var nc = new NatsConnection(opts);
await nc.ConnectAsync();

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

var handler = new EntityHandler(nc, cts.Token);

Console.WriteLine("Backend listening on NATS");

try
{
    await handler.ListenAsync();
}
catch (OperationCanceledException)
{
    // graceful shutdown
}

Console.WriteLine("Draining NATS connection...");
await nc.DisposeAsync();
