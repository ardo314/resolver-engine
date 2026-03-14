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

### Behaviour

A **behaviour** is a data contract defined as an interface in Engine.Core.

- `IBehaviour` — marker interface; all behaviours implement this.
- `IDataBehaviour<T> : IBehaviour` — a behaviour that holds typed data with async `InitDataAsync`, `GetDataAsync`, and `SetDataAsync` methods.
- `IPose : IDataBehaviour<Pose>` — position and rotation (`Vector3` + `Quaternion`).
- `IParent : IDataBehaviour<EntityId>` — parent-child entity relationship.

Behaviours are **interfaces only**; they carry no implementation. This allows different module workers to provide different storage backends (in-memory, database-backed, networked, etc.) for the same contract.

### BehaviourWorker

`BehaviourWorker<T>` (Engine.Module) is the abstract base class for module workers. It is generic over a behaviour interface `T : IBehaviour` and provides virtual lifecycle hooks:

- `OnAddedAsync(T behaviour, …)` — called when the behaviour is attached to an entity.
- `OnRemovedAsync(T behaviour, …)` — called when the behaviour is removed.

Concrete workers (e.g., `InMemoryPoseWorker`, `InMemoryParentWorker`) extend this base and implement the data access methods from `IDataBehaviour<T>`.

### World

`World` (Engine.Module) is the top-level container that creates entities via `CreateEntityAsync`.

## Project Dependency Graph

```
Engine.Core  (no dependencies)
    ↑
Engine.Module  ──references──▶ Engine.Core
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

1. **Engine.Backend** — the central server process. Hosts the `WorldService` which manages entity lifecycles over NATS.
2. **Engine.ModuleRuntime** — the module host process. Sets up a `CancellationTokenSource` tied to `Ctrl+C` and will load and run module workers, subscribing to NATS subjects for behaviour operations.

Modules run inside the ModuleRuntime process, not as separate executables.

## NATS Subject Conventions

All service endpoints are exposed via NATS micro-services (`NatsSvcServer`). Subjects follow the pattern `<service>.<operation>`.

### WorldService (`world`)

| Subject | Request | Reply | Description |
|---|---|---|---|
| `world.create` | empty | EntityId (Guid string) | Create a new entity |
| `world.destroy` | EntityId (Guid string) | `"ok"` or error | Destroy an existing entity |
| `world.exists` | EntityId (Guid string) | `"true"` / `"false"` | Check if an entity exists |
| `world.list` | empty | comma-separated EntityIds | List all entity IDs |

Errors are returned via NATS service error replies with a numeric code and description.

## Conventions

- All behaviour interfaces live in **Engine.Core** so they can be shared between backend and module code without circular dependencies.
- Module projects live under the `modules/` folder and reference Engine.Core + Engine.Module.
- Module worker classes are `partial` to support future source generation.
- Async-first API: all behaviour and entity operations return `Task` and accept `CancellationToken`.
