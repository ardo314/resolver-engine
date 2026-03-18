# Architecture

## Overview

Resolver Engine is an **Entity-Component-Behaviour** engine built with .NET 9 and C#. Entities are lightweight identifiers; behaviours define data contracts as interfaces; component structs declare which behaviours they provide via `[Has<T>]` attributes; module workers provide concrete storage and logic for those contracts. Communication between the backend and module runtimes uses **NATS** as the message transport with **MessagePack** serialization. A Roslyn-based source generator (**Engine.Generators**) eliminates boilerplate by generating client-side NATS proxies and worker-side dispatch code for behaviour interfaces.

## Solution Layout

```
Engine.sln
├── src/                              # Core libraries and executables
│   ├── Engine.Core                   # Shared contracts (IComponent, IBehaviour, value types, HasAttribute)
│   ├── Engine.Client                 # Client-side proxies (Entity, World)
│   ├── Engine.Generators             # Roslyn source generator (analyzer)
│   ├── Engine.Module                 # Module-side abstractions (ComponentWorker, IDataDispatch)
│   ├── Engine.ModuleRuntime          # Executable host for running modules
│   ├── Engine.Sandbox                # Console app for experimentation
│   └── Engine.Backend                # Central backend process
└── modules/                          # Pluggable module implementations
    ├── Modules.InMemoryPose.Core     # Marker struct for InMemoryPose component
    ├── Modules.InMemoryPose          # In-memory IPose component worker
    ├── Modules.InMemoryParent.Core   # Marker struct for InMemoryParent component
    └── Modules.InMemoryParent        # In-memory IParent component worker
```

### Planned / Referenced (not yet implemented)

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
| `MessagePack` | 3.1.4 | Engine.Backend, Engine.ModuleRuntime, module projects |
| `NATS.Net` | 2.7.2 | Engine.Backend, Engine.Module, Engine.ModuleRuntime |
| `Microsoft.CodeAnalysis.CSharp` | 4.12.0 | Engine.Generators |
| `Microsoft.CodeAnalysis.Analyzers` | 3.3.4 | Engine.Generators |

### VS Code Tasks

- **build** — `dotnet build Engine.sln`
- **publish** — `dotnet publish Engine.sln`
- **watch** — `dotnet watch run` (solution-level)

## Key Concepts

### Entity

An entity is a lightweight identity represented by `EntityId` (a `readonly record struct` wrapping a `Guid`). The `Entity` class in Engine.Client associates an `EntityId` with methods to add, remove, query, and retrieve components.

### EntityRepository

`EntityRepository` (Engine.Backend) is the central in-memory store for entity existence and per-entity component sets. Used by `EntityService`, providing a single source of truth for:

- Entity lifecycle — `Create`, `Destroy`, `Exists`, `ListAll`.
- Component tracking — `AddComponent`, `RemoveComponent`, `HasComponent`, `ListComponents`.

All operations are thread-safe via `ConcurrentDictionary`.

### Component

A **component** is a concrete marker struct that implements `IComponent` and declares which behaviours it provides via `[Has<T>]` attributes.

- `IComponent` — marker interface implemented by all component structs.

Components are structs that represent a named, deployable unit of functionality. They carry no data or logic themselves — they serve as type-level identifiers for adding/removing functionality to entities.

### Behaviour

A **behaviour** is a data/logic contract defined as an interface in Engine.Core.

- `IBehaviour` — marker interface; all behaviours implement this.
- `IDataBehaviour<T> : IBehaviour` — a convenience base for behaviours that hold typed data with async `GetDataAsync` and `SetDataAsync` methods.
- `IPose : IDataBehaviour<Pose>` — position and rotation (`Vector3` + `Quaternion`).
- `IParent : IDataBehaviour<EntityId>` — parent-child entity relationship.

Behaviours are **interfaces only**; they carry no implementation. Any interface extending `IBehaviour` can define arbitrary async methods (returning `Task` or `Task<T>`, with zero or one value parameter plus an optional `CancellationToken`). The source generator produces client-side proxies and worker-side dispatch code for all methods declared on a behaviour interface.

### Component Marker Structs and `Has<T>`

Each module defines a **component struct** in a `.Core` project that implements `IComponent` and declares which behaviour interfaces it provides via `[Has<T>]` attributes:

```csharp
[Has<IPose>]
public struct InMemoryPose : IComponent { }
```

A single struct can provide multiple behaviour interfaces:

```csharp
[Has<IPose>]
[Has<IParent>]
public struct InMemoryPoseAndParent : IComponent { }
```

Component structs serve three purposes:
1. **Client-side `AddComponentAsync<T>()`** — the client uses the struct type to add components: `entity.AddComponentAsync<InMemoryPose>()`.
2. **Worker type argument** — workers are generic over the component struct: `ComponentWorker<InMemoryPose>`.
3. **Source generator input** — the generator reads `[Has<>]` attributes to determine which behaviour interfaces the worker must implement and generates dispatch code accordingly.

Component structs live in `Modules.<Name>.Core` projects so they can be shared between client code and module implementations without pulling in worker dependencies.

### `HasAttribute<T>`

`HasAttribute<T>` (Engine.Core) is a generic attribute with an abstract base class for runtime reflection:

```csharp
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
public abstract class HasAttribute : Attribute
{
    public abstract Type ComponentType { get; }
}

public sealed class HasAttribute<T> : HasAttribute where T : IBehaviour
{
    public override Type ComponentType => typeof(T);
}
```

### ComponentWorker

`ComponentWorker<T>` (Engine.Module) is the abstract base class for module workers. It is generic over a component struct `T : struct, IComponent` and provides:

- `EntityId` property — set by the module runtime after construction to identify which entity this worker belongs to.
- `OnAddedAsync(CancellationToken)` — called when the component is attached to an entity.
- `OnRemovedAsync(CancellationToken)` — called when the component is removed.

Concrete workers (e.g., `InMemoryPoseWorker`, `InMemoryParentWorker`) extend this base and implement the methods from their behaviour interfaces (as determined by the `[Has<>]` attributes on the component struct). One worker instance is created per `(EntityId, componentStruct)` pair. When a component struct declares multiple `[Has<>]` interfaces, the worker must implement all of them.

### IDataDispatch

`IDataDispatch` (Engine.Module) is a non-generic interface implemented by generated worker partial classes:

```csharp
public interface IDataDispatch
{
    Task<ReadOnlyMemory<byte>> DispatchAsync(string componentName, string methodName, ReadOnlyMemory<byte> payload, CancellationToken ct);
}
```

It provides a single entry point for the ModuleRuntime to invoke any behaviour method on a worker without reflection. The `componentName` parameter disambiguates methods when a worker handles multiple behaviour interfaces with overlapping method names. The source generator emits a nested `switch` over component name and method name, deserializes parameters with MessagePack, calls the worker's concrete method via an interface cast, and serializes the return value.

### Source Generator (Engine.Generators)

`ComponentProxyGenerator` is a Roslyn `IIncrementalGenerator` (`netstandard2.0`, referenced as an analyzer) that produces two kinds of generated code:

- **Worker-side partial classes** — for each `partial` class inheriting `ComponentWorker<T>`, the generator reads `[Has<>]` attributes from the component struct `T`, emits a partial that adds all behaviour interfaces to the class declaration, and implements `IDataDispatch` with a nested component/method dispatch switch. Interface casts are used in the dispatch to correctly route method calls when multiple interfaces share method names.
- **Client-side proxy classes** — for each interface extending `IBehaviour`, the generator emits a proxy class (e.g., `PoseProxy` for `IPose`) that implements the behaviour interface and forwards each method call over NATS request-reply to the ModuleRuntime.

Proxy classes accept an `EntityId` and `INatsConnection` and can be obtained via `Entity.GetComponent<T>()` where `T` is a behaviour interface.

### World

`World` (Engine.Client) is the client-side proxy to the backend `EntityService`. It accepts an `INatsConnection` and forwards entity lifecycle operations over NATS request-reply:

- `CreateEntityAsync` → `entity.create` — returns a local `Entity` handle.
- `DestroyEntityAsync` → `entity.destroy` — removes an entity from the backend.
- `EntityExistsAsync` → `entity.exists` — checks if an entity exists.
- `ListEntitiesAsync` → `entity.list` — returns all known entity IDs.

## Project Dependency Graph

```
Engine.Core  (no dependencies)
    ↑
Engine.Generators  ──packages──▶ Microsoft.CodeAnalysis.CSharp

Engine.Client  ──references──▶ Engine.Core
               ──packages────▶ NATS.Net
    ↑
Engine.Module  ──references──▶ Engine.Core, Engine.Client
    ↑
Engine.ModuleRuntime  ──references──▶ Engine.Core, Engine.Module
                      ──packages────▶ NATS.Net, MessagePack

Engine.Sandbox  ──references──▶ Engine.Core, Engine.Client, Modules.InMemoryParent.Core
                ──packages────▶ NATS.Net

Engine.Backend  ──references──▶ Engine.Core
                ──packages────▶ NATS.Net, MessagePack

Modules.InMemoryPose.Core   ──references──▶ Engine.Core
Modules.InMemoryParent.Core ──references──▶ Engine.Core

Modules.InMemoryPose   ──references──▶ Engine.Core, Engine.Module, Modules.InMemoryPose.Core
                       ──analyzer────▶ Engine.Generators
                       ──packages────▶ MessagePack
Modules.InMemoryParent ──references──▶ Engine.Core, Engine.Module, Modules.InMemoryParent.Core
                       ──analyzer────▶ Engine.Generators
                       ──packages────▶ MessagePack
```

## Transport & Serialization

- **NATS** (`NATS.Net` package) is the messaging backbone connecting the backend to module runtimes.
- **MessagePack** is the wire format for component data exchanged over NATS.
- The Engine.ModuleRuntime process hosts module workers and bridges NATS messages to `ComponentWorker` lifecycle methods.

## Process Model

Two executable projects exist:

1. **Engine.Backend** — the central server process. Hosts the `EntityService` (entity lifecycles and component tracking) over NATS. Acts as a two-phase orchestrator: when a component is added or removed, the backend first sends a NATS request to the module runtime and only commits the change to the entity registry if the runtime responds successfully.
2. **Engine.ModuleRuntime** — the module host process. Connects to NATS, discovers module DLLs, builds a type registry of `ComponentWorker<T>` types, reads `[Has<>]` attributes from their component structs, and subscribes to `worker.create.<structName>` and `worker.remove.<structName>` subjects to create/destroy worker instances on demand. Additionally subscribes to `component.<interfaceName>.*` subjects to dispatch behaviour method calls to live workers via their `IDataDispatch` implementation.

### Component Add Flow

1. A client calls `Entity.AddComponentAsync<T>()`, which sends a request to `entity.add-component` with the component struct name.
2. The backend validates the request (entity exists, component not already added).
3. The backend sends a NATS request to `worker.create.<structName>` with the `EntityId` as payload.
4. The module runtime creates a new `ComponentWorker<T>` instance, sets its `EntityId` property, calls `OnAddedAsync`, registers the worker for all behaviour interfaces declared by the struct's `[Has<>]` attributes, and replies `"ok"`.
5. On success, the backend registers the component in the `EntityRepository` and replies `"ok"` to the caller.
6. On failure (no responders, timeout, or error), the backend replies with an error and does **not** register the component.

### Entity Destroy Flow

1. A client calls `World.DestroyEntityAsync(id)`, which sends a request to `entity.destroy`.
2. The backend removes the entity from the `EntityRepository`, obtaining the list of components that were attached.
3. For each component, the backend sends a `worker.remove.<structName>` request to the module runtime, which calls `OnRemovedAsync` on the worker and removes it.
4. The backend replies `"ok"` to the caller.

### Component Remove Flow

Same two-phase pattern: `entity.remove-component` → `worker.remove.<structName>` → `OnRemovedAsync` → remove from repository.

### Module Loading

At startup the ModuleRuntime scans `{AppContext.BaseDirectory}/modules/` for `.dll` files. For each assembly it finds, it reflects over exported types and builds a **type registry** (`Dictionary<string, Type>`) mapping each component struct name (e.g. `"InMemoryPose"`) to the concrete `ComponentWorker<T>` type that handles it. It also builds a **component mapping** (`Dictionary<string, string>`) from behaviour interface name (e.g. `"IPose"`) to component struct name, by reading `[Has<>]` attributes from each component struct. No worker instances are created eagerly — they are instantiated on demand when `worker.create.<structName>` requests arrive.

Workers are created via parameterless constructors (`Activator.CreateInstance`). Live instances are tracked in two dictionaries:
- `(EntityId, structName) → worker` for lifecycle management (create/remove).
- `(EntityId, behaviourInterfaceName) → worker` for method dispatch routing.

When a worker handles multiple behaviour interfaces, the same instance appears in the dispatch dictionary under each interface name.

To deploy a module, copy its build output (DLL + dependencies, including the `.Core` project) into the `modules/` sub-directory of the ModuleRuntime publish output.

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
| `entity.add-component` | `entityId:componentName` | `"ok"` or error | Add a component to an entity (triggers `worker.create`) |
| `entity.remove-component` | `entityId:componentName` | `"ok"` or error | Remove a component from an entity (triggers `worker.remove`) |
| `entity.has-component` | `entityId:componentName` | `"true"` / `"false"` or error | Check if an entity has a component |
| `entity.list-components` | EntityId (Guid string) | comma-separated component names | List components on an entity |

### ModuleRuntime (worker lifecycle)

| Subject | Request | Reply | Description |
|---|---|---|---|
| `worker.create.<structName>` | EntityId (Guid string) | `"ok"` or error | Create a worker instance for the given entity and marker struct |
| `worker.remove.<structName>` | EntityId (Guid string) | `"ok"` or error | Remove the worker instance for the given entity and marker struct |

### Component Method Dispatch (`component`)

| Subject | Request | Reply | Description |
|---|---|---|---|
| `component.<interfaceName>.<methodName>` | EntityId (Guid string) or MessagePack parameter (with EntityId in `EntityId` header) | MessagePack result or `"ok"` | Invoke a component method on the worker for the given entity |

When a method has no value parameter, the `EntityId` is sent as a Guid string in the request payload. When a method has one value parameter, the parameter is serialized as MessagePack in the payload and the `EntityId` is sent in a NATS header named `EntityId`.

Examples: `component.IPose.GetDataAsync`, `component.IPose.SetDataAsync`, `component.IParent.GetDataAsync`.

Errors are returned via NATS service error replies with a numeric code and description, or as plain string error messages from the module runtime.

## Conventions

- All behaviour interfaces live in **Engine.Core** so they can be shared between backend and module code without circular dependencies.
- Each module has a **`.Core` project** (e.g. `Modules.InMemoryPose.Core`) containing the component struct with `[Has<>]` attributes and `IComponent` implementation. This project is shared between client and worker code.
- Module worker projects live under the `modules/` folder and reference Engine.Core, Engine.Module, and their `.Core` project.
- Module worker classes are `partial` to support source generation.
- Async-first API: all component and entity operations return `Task` and accept `CancellationToken`.
- Component method constraints: must return `Task` or `Task<T>`, accept 0 or 1 value parameter plus optional `CancellationToken`.
- Generated proxy naming convention: interface name with leading `I` stripped plus `Proxy` suffix (e.g., `IPose` → `PoseProxy`).
- `Entity.GetComponent<T>()` resolves proxy types by naming convention at runtime, where `T` is a behaviour interface (constrained to `IBehaviour`).
- `Entity.AddComponentAsync<T>()` takes a component struct type (constrained to `struct, IComponent`); the system maps it to the correct worker via the struct name.
