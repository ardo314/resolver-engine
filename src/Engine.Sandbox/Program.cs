using Engine.Client;
using Engine.Core;
using NATS.Client.Core;

using InMemoryParentComponent = InMemoryParent.InMemoryParent;

Console.WriteLine("Engine.Sandbox starting…");

await using var nats = new NatsConnection();
await nats.ConnectAsync();

var world = new World(nats);

// ── Try things out below this line ──────────────────────────────────────

Console.WriteLine("Connected to NATS. Ready to experiment!");

// Example: create an entity
var entity = await world.CreateEntityAsync();
Console.WriteLine($"Created entity {entity.Id}");

await entity.AddComponentAsync<InMemoryParentComponent>();
var parent = entity.GetComponentProxy<ParentProxy>();

var components = await entity.ListComponentsAsync();
Console.WriteLine($"Entity {entity.Id} components: {string.Join(", ", components)}");

var entities = await world.ListEntitiesAsync();
Console.WriteLine($"Current entities: {string.Join(", ", entities)}");

var exists = await world.EntityExistsAsync(entity.Id);
Console.WriteLine($"Entity {entity.Id} exists: {exists}");

var exists2 = await world.EntityExistsAsync(EntityId.New());
Console.WriteLine($"Random entity exists: {exists2}");

await world.DestroyEntityAsync(entity.Id);
Console.WriteLine($"Destroyed entity {entity.Id}");

Console.WriteLine("Engine.Sandbox finished.");
