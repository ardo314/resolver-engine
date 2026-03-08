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
└── Directory.Build.props      # Shared build settings (net9.0, nullable, warnings-as-errors)
```

### Engine.Core

The source-of-truth for the system's contract surface. Contains:

- **Interfaces** that define Component and Behaviour contracts. These are the input for source generation.
- **Shared types** used by both client and server (entity identifiers, component descriptors, common enums/value objects).
- **Attributes** that annotate interfaces for the source generator (e.g., marking a method as fire-and-forget vs. request/reply).

Engine.Core has **no dependency** on NATS, MessagePack, or any infrastructure concern. It is a pure contract/types library.

### Engine.Backend

Server-side runtime. Responsibilities:

- **Entity lifecycle** — creation, destruction, ownership.
- **Component management** — adding, removing, and querying Components on Entities.
- **Behaviour hosting** — running Behaviour implementations as services that listen on NATS subjects.
- **Server stubs** — generated from Engine.Core interfaces by the source generator; the developer implements the stub to provide behaviour logic.

References: `Engine.Core`

### Engine.Client

Developer-facing client API. Provides an ergonomic programming model such as:

```csharp
var entity = await world.CreateEntity();
await entity.AddComponent<MyComponent>();
```

- **Client proxies** — generated from Engine.Core interfaces by the source generator; each proxy serializes calls with MessagePack and sends them over NATS.
- **Connection management** — maintains the NATS connection and exposes it through a simple API.

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
- **Subject conventions** — subjects are derived deterministically from the interface and method names defined in Engine.Core (exact convention TBD during implementation).

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
| Formatter               | CSharpier 1.2.6 (format on save) |
| Dev container            | `mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim` + NATS server |
