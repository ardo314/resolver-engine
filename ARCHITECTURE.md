# Architecture

## Overview

Resolver Engine is an **Entity-Behaviour** engine built with .NET 9 and C#. Entities are lightweight identifiers; behaviours define data contracts as interfaces; module workers provide concrete storage and logic for those contracts. Communication between the backend and module runtimes uses **NATS** as the message transport with **MessagePack** serialization. A Roslyn-based source generator is planned to reduce boilerplate in module projects.

## Solution Layout

```
Engine.sln
├── src/                         # Core libraries and executables
│   ├── Engine.Core              # Shared contracts (interfaces, value types)
│   ├── Engine.Module            # Module-side abstractions (Entity, World, BehaviourWorker)
│   ├── Engine.ModuleRuntime     # Executable host for running modules
│   └── Engine.Backend           # Central backend process
└── modules/                     # Pluggable module implementations
    ├── Modules.InMemoryPose     # In-memory IPose behaviour worker
    └── Modules.InMemoryParent   # In-memory IParent behaviour worker
```

### Planned / Referenced (not yet implemented)

- **Engine.Generators** — Roslyn source generator (`OutputItemType=Analyzer`), referenced by Engine.ModuleRuntime. Will generate boilerplate for module workers.
- **Engine.Math** — Math utilities, referenced by Modules.InMemoryPose.
- **Engine.Hierarchy** — Hierarchy utilities, referenced by Modules.InMemoryParent.

## Build & Tooling

| Setting | Value |
|---|---|
| SDK | .NET 9 (`global.json` → `9.0.100`, `rollForward: latestFeature`) |
| Target framework | `net9.0` (set in `Directory.Build.props`) |
| Nullable reference types | Enabled globally |
| Implicit usings | Enabled globally |
| Warnings as errors | Enabled globally |
| Package management | Central (`Directory.Packages.props`) |

### Central Package Versions

| Package | Version | Used by |
|---|---|---|
| `MessagePack` | 3.1.4 | Engine.Backend, Engine.ModuleRuntime |
| `NATS.Net` | 2.7.2 | Engine.Backend, Engine.ModuleRuntime |
| `Microsoft.CodeAnalysis.CSharp` | 4.12.0 | Engine.Generators (planned) |
| `Microsoft.CodeAnalysis.Analyzers` | 3.3.4 | Engine.Generators (planned) |

### VS Code Tasks

- **build** — `dotnet build Engine.sln`
- **publish** — `dotnet publish Engine.sln`
- **watch** — `dotnet watch run` (solution-level)

## Key Concepts

### Entity

An entity is a lightweight identity represented by `EntityId` (a `readonly record struct` wrapping a `Guid`). The `Entity` class in Engine.Module associates an `EntityId` with methods to add, remove, query, and retrieve behaviours.

### EntityRepository

`EntityRepository` (Engine.Backend) is the central in-memory store for entity existence and per-entity behaviour sets. Used by `EntityService`, providing a single source of truth for:

- Entity lifecycle — `Create`, `Destroy`, `Exists`, `ListAll`.
- Behaviour tracking — `AddBehaviour`, `RemoveBehaviour`, `HasBehaviour`, `ListBehaviours`.

All operations are thread-safe via `ConcurrentDictionary`.

### Behaviour

A **behaviour** is a data contract defined as an interface in Engine.Core.

- `IBehaviour` — marker interface; all behaviours implement this.
- `IDataBehaviour<T> : IBehaviour` — a behaviour that holds typed data with async `InitDataAsync`, `GetDataAsync`, and `SetDataAsync` methods.
- `IPose : IDataBehaviour<Pose>` — position and rotation (`Vector3` + `Quaternion`).
- `IParent : IDataBehaviour<EntityId>` — parent-child entity relationship.

Behaviours are **interfaces only**; they carry no implementation. This allows different module workers to provide different storage backends (in-memory, database-backed, networked, etc.) for the same contract.

### BehaviourWorker

`BehaviourWorker<T>` (Engine.Module) is the abstract base class for module workers. It is generic over a behaviour interface `T : IBehaviour` and provides:

- `EntityId` property — set by the module runtime after construction to identify which entity this worker belongs to.
- `OnAddedAsync(CancellationToken)` — called when the behaviour is attached to an entity.
- `OnRemovedAsync(CancellationToken)` — called when the behaviour is removed.

Concrete workers (e.g., `InMemoryPoseWorker`, `InMemoryParentWorker`) extend this base and implement the data access methods from `IDataBehaviour<T>`. One worker instance is created per `(EntityId, behaviour)` pair.

### World

`World` (Engine.Module) is the client-side proxy to the backend `EntityService`. It accepts an `INatsConnection` and forwards entity lifecycle operations over NATS request-reply:

- `CreateEntityAsync` → `entity.create` — returns a local `Entity` handle.
- `DestroyEntityAsync` → `entity.destroy` — removes an entity from the backend.
- `EntityExistsAsync` → `entity.exists` — checks if an entity exists.
- `ListEntitiesAsync` → `entity.list` — returns all known entity IDs.

## Project Dependency Graph

```
Engine.Core  (no dependencies)
    ↑
Engine.Module  ──references──▶ Engine.Core
               ──packages────▶ NATS.Net
    ↑
Engine.ModuleRuntime  ──references──▶ Engine.Core
                      ──analyzer────▶ Engine.Generators (planned)
                      ──packages────▶ NATS.Net, MessagePack

Engine.Backend  ──references──▶ Engine.Core
                ──packages────▶ NATS.Net, MessagePack

Modules.InMemoryPose   ──references──▶ Engine.Core, Engine.Module, Engine.Math (planned)
Modules.InMemoryParent ──references──▶ Engine.Core, Engine.Module, Engine.Hierarchy (planned)
```

## Transport & Serialization

- **NATS** (`NATS.Net` package) is the messaging backbone connecting the backend to module runtimes.
- **MessagePack** is the wire format for behaviour data exchanged over NATS.
- The Engine.ModuleRuntime process hosts module workers and bridges NATS messages to `BehaviourWorker` lifecycle methods.

## Process Model

Two executable projects exist:

1. **Engine.Backend** — the central server process. Hosts the `EntityService` (entity lifecycles and behaviour tracking) over NATS. Acts as a two-phase orchestrator: when a behaviour is added or removed, the backend first sends a NATS request to the module runtime and only commits the change to the entity registry if the runtime responds successfully.
2. **Engine.ModuleRuntime** — the module host process. Connects to NATS, discovers module DLLs, builds a type registry of `BehaviourWorker<T>` types, and subscribes to `worker.create.<name>` and `worker.remove.<name>` subjects to create/destroy worker instances on demand.

### Behaviour Add Flow

1. A module calls `Entity.AddBehaviourAsync<T>()`, which sends a request to `entity.add-behaviour`.
2. The backend validates the request (entity exists, behaviour not already added).
3. The backend sends a NATS request to `worker.create.<behaviourName>` with the `EntityId` as payload.
4. The module runtime creates a new `BehaviourWorker<T>` instance, sets its `EntityId` property, calls `OnAddedAsync`, and replies `"ok"`.
5. On success, the backend registers the behaviour in the `EntityRepository` and replies `"ok"` to the caller.
6. On failure (no responders, timeout, or error), the backend replies with an error and does **not** register the behaviour.

### Behaviour Remove Flow

Same two-phase pattern: `entity.remove-behaviour` → `worker.remove.<behaviourName>` → `OnRemovedAsync` → remove from repository.

### Module Loading

At startup the ModuleRuntime scans `{AppContext.BaseDirectory}/modules/` for `.dll` files. For each assembly it finds, it reflects over exported types and builds a **type registry** (`Dictionary<string, Type>`) mapping each behaviour name (e.g. `"IPose"`) to the concrete `BehaviourWorker<T>` type that handles it. No worker instances are created eagerly — they are instantiated on demand when `worker.create.<name>` requests arrive.

Workers are created via parameterless constructors (`Activator.CreateInstance`). Live instances are tracked in a dictionary keyed by `(EntityId, behaviourName)` so they can be looked up for removal.

To deploy a module, copy its build output (DLL + dependencies) into the `modules/` sub-directory of the ModuleRuntime publish output.

Modules run inside the ModuleRuntime process, not as separate executables.

## NATS Subject Conventions

All service endpoints are exposed via NATS micro-services (`NatsSvcServer`). Subjects follow the pattern `<service>.<operation>`.

### EntityService (`entity`)

| Subject | Request | Reply | Description |
|---|---|---|---|
| `entity.create` | empty | EntityId (Guid string) | Create a new entity |
| `entity.destroy` | EntityId (Guid string) | `"ok"` or error | Destroy an existing entity |
| `entity.exists` | EntityId (Guid string) | `"true"` / `"false"` | Check if an entity exists |
| `entity.list` | empty | comma-separated EntityIds | List all entity IDs |
| `entity.add-behaviour` | `entityId:behaviourName` | `"ok"` or error | Add a behaviour to an entity (triggers `worker.create`) |
| `entity.remove-behaviour` | `entityId:behaviourName` | `"ok"` or error | Remove a behaviour from an entity (triggers `worker.remove`) |
| `entity.has-behaviour` | `entityId:behaviourName` | `"true"` / `"false"` or error | Check if an entity has a behaviour |
| `entity.list-behaviours` | EntityId (Guid string) | comma-separated behaviour names | List behaviours on an entity |

### ModuleRuntime (worker lifecycle)

| Subject | Request | Reply | Description |
|---|---|---|---|
| `worker.create.<behaviourName>` | EntityId (Guid string) | `"ok"` or error | Create a worker instance for the given entity and behaviour |
| `worker.remove.<behaviourName>` | EntityId (Guid string) | `"ok"` or error | Remove the worker instance for the given entity and behaviour |

Errors are returned via NATS service error replies with a numeric code and description, or as plain string error messages from the module runtime.

## Conventions

- All behaviour interfaces live in **Engine.Core** so they can be shared between backend and module code without circular dependencies.
- Module projects live under the `modules/` folder and reference Engine.Core + Engine.Module.
- Module worker classes are `partial` to support future source generation.
- Async-first API: all behaviour and entity operations return `Task` and accept `CancellationToken`.
