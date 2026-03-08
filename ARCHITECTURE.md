# Architecture

## Overview

Engine is a distributed Entity-Component framework for .NET 9. It draws loose inspiration from Unity3D's programming model — Entities own Components and Behaviours — but is designed from the ground up as a distributed system where each Behaviour runs as its own service.

All inter-service communication flows through **NATS** (request/reply, pub/sub). All messages are serialized with **MessagePack**. Client and server code is generated at compile time from shared interface definitions using **Roslyn incremental source generators**.

## Solution Structure

```
Engine.sln
├── src/
│   ├── Engine.Core/          # Shared types, contracts, interfaces
│   ├── Engine.Backend/        # Server-side entity lifecycle & hosting
│   └── Engine.Client/         # Ergonomic client API
├── Directory.Build.props      # Shared build settings (net9.0, nullable, warnings-as-errors)
└── Directory.Packages.props   # Central package version management
```

### Engine.Core

The source-of-truth for the system's contract surface. Contains:

- **`EntityId`** — a `readonly record struct` wrapping a `Guid` that uniquely identifies an Entity.
- **`IComponent`** — marker interface for all components (data attached to an Entity).
- **`IBehaviour`** — marker interface for all behaviours (remote logic operating on components).
- **`IEntity`** — contract for entity operations: `AddComponentAsync`, `RemoveComponentAsync`, `GetComponentAsync`, `HasComponentAsync`.
- **`IWorld`** — contract for entity lifecycle: `CreateEntityAsync`, `DestroyEntityAsync`, `GetEntityAsync`.
- **Attributes** that annotate interfaces for the source generator (e.g., marking a method as fire-and-forget vs. request/reply) — planned.

Engine.Core has **no dependency** on NATS, MessagePack, or any infrastructure concern. It is a pure contract/types library.

### Engine.Backend

Server-side runtime. Responsibilities:

- **Entity lifecycle** — creation, destruction, ownership via `World` (implements `IWorld`).
- **Entity storage** — `EntityStore` provides thread-safe in-memory storage of `EntityRecord` instances.
- **Component management** — `EntityRecord` manages adding, removing, and querying `IComponent` instances on an entity. `Entity` (implements `IEntity`) wraps a record into the core interface.
- **Behaviour hosting** — running Behaviour implementations as services that listen on NATS subjects (planned).
- **Server stubs** — generated from Engine.Core interfaces by the source generator (planned).

References: `Engine.Core`

### Engine.Client

Developer-facing client API. Provides an ergonomic programming model such as:

```csharp
var entity = await world.CreateEntity();
await entity.AddComponent<MyComponent>();
```

- **`EngineConnection`** — manages the NATS connection lifecycle (`IAsyncDisposable`).
- **`World`** — implements `IWorld`; communicates with the backend over NATS.
- **`Entity`** — implements `IEntity`; publishes component operations to NATS subjects.
- **Client proxies** — generated from Engine.Core interfaces by the source generator (planned).

References: `Engine.Core`

## Key Concepts

| Concept       | Description |
|---------------|-------------|
| **Entity**    | A uniquely identified object in the world. Analogous to Unity's `GameObject`. Has no behaviour of its own; it is a container for Components. |
| **Component** | Data or state attached to an Entity. Can be added and removed at runtime. |
| **Behaviour** | Logic that operates on Components. Each Behaviour is a remote service — its interface lives in Core, its implementation in Backend, and its proxy in Client. |

## Communication

**NATS** is the primary transport for all inter-service communication.

- **Request/Reply** — used for operations that return a result (e.g., creating an entity, querying a component).
- **Publish/Subscribe** — used for broadcasting events (e.g., component added, entity destroyed).
- **Subject conventions** — subjects follow the pattern `entity.{id}.{operation}` (e.g., `entity.{id}.component.add.{TypeName}`, `entity.{id}.destroy`). Derived deterministically from entity IDs and component type names.

NATS is installed in the dev container (v2.12.4) for local development.

## Serialization

**MessagePack** is used for all message serialization/deserialization.

- Compact binary format — low overhead for high-frequency messaging.
- All types crossing the wire must be MessagePack-serializable.
- Source-generated formatters are preferred over reflection-based serialization for performance.

## Code Generation

**Roslyn incremental source generators** read the interfaces defined in `Engine.Core` and emit:

1. **Client proxies** (into `Engine.Client`) — each interface method becomes a NATS request serialized with MessagePack.
2. **Server stubs** (into `Engine.Backend`) — base classes or dispatch handlers that deserialize incoming NATS messages and invoke the developer's implementation.

The generator project will be added to the solution as `Engine.Generators` (or similar) when implementation begins.

## Build & Tooling

| Tool / Setting          | Detail |
|-------------------------|--------|
| .NET SDK                | 9.0.100 (`global.json`, `rollForward: latestFeature`) |
| Target framework        | `net9.0` |
| Nullable reference types | Enabled |
| Implicit usings          | Enabled |
| Warnings as errors       | Enabled |
| Package management       | Central (`Directory.Packages.props`) |
| Formatter               | CSharpier 1.2.6 (format on save) |
| Dev container            | `mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim` + NATS server |

### NuGet Packages

Versions are pinned in `Directory.Packages.props`:

| Package     | Version | Used By |
|-------------|---------|---------|
| NATS.Net    | 2.7.2   | Backend, Client |
| MessagePack | 3.1.4   | Backend, Client |
