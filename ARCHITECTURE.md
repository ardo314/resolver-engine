# Architecture

## Overview

Engine is a distributed Entity-Component framework for .NET 9. It draws loose inspiration from Unity3D's programming model — Entities own Components and Behaviours — but is designed from the ground up as a distributed system where each Behaviour runs as its own service.

All inter-service communication flows through **NATS** (request/reply, pub/sub). All messages are serialized with **MessagePack**. Client and server code is generated at compile time from shared interface definitions using **Roslyn incremental source generators**.

## Solution Structure

```
Engine.sln
├── src/
│   ├── Engine.Core/          # Shared types, contracts, extension interfaces
│   ├── Engine.Generators/     # Roslyn incremental source generator (netstandard2.0)
│   ├── Engine.Hierarchy/      # Entity relationship components (Parent, IParent)
│   ├── Engine.Math/           # Math primitives and component contracts (Pose, IPose)
│   ├── Engine.ModuleRuntime/   # Executable host — assembly scanning, NATS, dispatch
│   └── Example/               # Example extension demonstrating the component model
├── modules/
│   ├── Modules.InMemoryPose/  # In-memory implementation of IPose
│   ├── Modules.DatabasePose/  # Database-backed implementation of IPose
│   └── Modules.InMemoryParent/# In-memory implementation of IParent
├── Directory.Build.props      # Shared build settings (net9.0, nullable, warnings-as-errors)
└── Directory.Packages.props   # Central package version management
```

### Engine.Core

The source-of-truth for the system's contract surface. Contains:

- **`EntityId`** — a `readonly record struct` wrapping a `Guid` that uniquely identifies an Entity.
- **`IComponent`** — base interface for all components. Defines `OnRemoveAsync(CancellationToken)`. Each component instance belongs to exactly one entity.
- **`IComponent<TData>`** — generic interface extending `IComponent` that defines typed data access for a component: `OnAddAsync`, `GetAsync`, `SetAsync`, plus an `Updated` event that implementations must fire.
- **`Component`** — non-generic abstract base class for component implementations. Provides a `public Entity Entity` property, set by the framework before `OnAddAsync` is called.
- **`Component<TContract>`** — generic abstract base class extending `Component`. Users subclass this with their contract interface as the type parameter (e.g., `class InMemoryPose : Component<IPose>`). Each entity gets its own instance. The source generator detects subclasses, resolves `TData` from the contract, and emits a partial class with event infrastructure and the contract interface marker.
- **`ComponentHandle<TData>`** — entity-bound view of a component. Wraps `IComponent<TData>` + `Entity`, providing `GetAsync()`/`SetAsync()` and `Updated`/`Removed` events. Returned by `Entity.AddComponentAsync` and `Entity.GetComponentAsync`.
- **`IComponentHandle`** — non-generic base interface for `ComponentHandle<TData>`, enabling single-type-parameter lookups.
- **`Entity`** — concrete class representing an entity in the world. Contains the component registry that enforces one-implementation-per-contract uniqueness. Creates per-entity component instances and injects the `Entity` reference before calling `OnAddAsync`. Provides `AddComponentAsync`, `GetComponentAsync`, `HasComponentAsync`, `RemoveComponentAsync`, and `GetBehaviourAsync`. Exposes `ComponentAdded` and `ComponentRemoved` events for lifecycle tracking.
- **`IBehaviour`** — marker interface for all behaviours (remote logic operating on components).
- **`IWorld`** — contract for entity lifecycle: `CreateEntityAsync`, `DestroyEntityAsync`, `GetEntityAsync`. Returns `Entity` instances.
- **`Plugin`** — abstract base class for user plugins. The runtime sets `World` before calling `OnStartAsync`/`OnStopAsync`.
- **`IExtension`** — entry point for extensions loaded by the runtime. Each extension assembly implements this interface to register its components, behaviours, and plugins.
- **`IExtensionRegistrar`** — provided by the runtime to extensions during registration. Extensions call `AddComponent<T>()`, `AddBehaviour<TContract, TImpl>()`, and `AddPlugin<T>()` to declare their types.

Engine.Core has **no dependency** on NATS, MessagePack, or any infrastructure concern. It is a pure contract/types library.

### Engine.Math

Math primitives and component contracts that build on Engine.Core. Contains:

- **`Pose`** — a value type holding a `Vector3 Position` and `Quaternion Rotation`.
- **`IPose`** — component contract interface extending `IComponent<Pose>`.

References: `Engine.Core`

### Engine.Hierarchy

Entity relationship components that build on Engine.Core. Contains:

- **`Parent`** — a value type holding an `EntityId ParentId` referencing the parent entity.
- **`IParent`** — component contract interface extending `IComponent<Parent>`.

References: `Engine.Core`

### Engine.Generators

A **Roslyn incremental source generator** targeting `netstandard2.0`. Referenced as an analyzer by consuming projects (Engine.ModuleRuntime and any extension). It does not ship as a runtime dependency — only as a build-time code generator.

The generator scans each consuming compilation for:

1. **Interfaces extending `IBehaviour`** → emits **client proxy** classes (one per interface) that serialize method calls to NATS with MessagePack.
2. **Concrete classes implementing `IBehaviour`-derived interfaces** → emits **server dispatch stubs** (one per class) that subscribe to NATS subjects, deserialize requests, call the implementation, and reply.
3. **Types implementing `IComponent`** → emits a **`EngineComponentResolver`** that lists all component types and provides pre-configured `MessagePackSerializerOptions`.
4. **Classes extending `Component<TContract>`** → emits **partial classes** that add the contract interface, an `Updated` event, and a `RaiseUpdated` helper method.

Internal structure:

- `EngineGenerator` — the `IIncrementalGenerator` entry point; registers four pipelines.
- `Models/GeneratorModels.cs` — equatable data types (`BehaviourInterfaceInfo`, `BehaviourImplInfo`, `ComponentInfo`, `ComponentImplInfo`, `MethodInfo`, `ParameterInfo`) that carry symbol info through incremental pipelines.
- `Emitters/ClientProxyEmitter.cs` — generates `{Name}Proxy` classes from behaviour interfaces.
- `Emitters/ServerStubEmitter.cs` — generates `{Name}Dispatcher` classes from behaviour implementations.
- `Emitters/MessagePackEmitter.cs` — generates `EngineComponentResolver` from component types.
- `Emitters/ComponentEmitter.cs` — generates partial classes for `Component<TContract>` subclasses with event infrastructure.
- `Helpers/SymbolExtensions.cs` — Roslyn symbol utilities (e.g., `IsBehaviourInterface()`, `IsComponentImplementation()`, `IsComponentSubclass()`, `GetComponentContractType()`, `GetComponentDataType()`).
- `Helpers/SubjectNaming.cs` — deterministic NATS subject derivation: `behaviour.{TypeName}.{MethodName}` (strips leading `I`, strips `Async` suffix).

References: `Microsoft.CodeAnalysis.CSharp`, `Microsoft.CodeAnalysis.Analyzers` (both `PrivateAssets="all"`)

### Engine.ModuleRuntime

An executable host process — pure infrastructure, zero domain logic. Responsibilities:

- **Assembly scanning** — `ExtensionLoader` discovers and loads extension DLLs from a configurable directory (default `/app/extensions`), finds all `IExtension` implementations.
- **Extension registration** — `ExtensionRegistrar` (implements `IExtensionRegistrar`) collects component, behaviour, and plugin type registrations from each extension.
- **NATS connection** — `EngineHost` manages the NATS connection lifecycle.
- **Plugin lifecycle** — `EngineHost` instantiates registered `Plugin` types, injects the world, calls `OnStartAsync` at startup and `OnStopAsync` during graceful shutdown.
- **Message dispatch** — routes incoming NATS messages to the appropriate behaviour implementation via generated dispatcher stubs.
- **MessagePack setup** — uses the generated `EngineComponentResolver` to configure serialization.

The runtime is designed to be packaged as a container image. Extensions are baked into the image at build time:

```dockerfile
FROM engine-runtime:latest
COPY MyExtension/bin/Release/net9.0/publish/ /app/extensions/
```

The generator is referenced as an analyzer:
```xml
<ProjectReference Include="..\Engine.Generators\Engine.Generators.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

References: `Engine.Core`; Analyzer: `Engine.Generators`; Packages: `NATS.Net`, `MessagePack`

### Modules

The `modules/` directory contains concrete component implementations — swappable storage strategies that are separate from the contract definitions in `src/`. Each module references its contract project and the source generator.

- **`Modules.InMemoryPose`** — in-memory per-entity implementation of `IPose`. References: `Engine.Core`, `Engine.Math`; Analyzer: `Engine.Generators`
- **`Modules.DatabasePose`** — database-backed per-entity implementation of `IPose`. Also defines the `IDatabase` behaviour contract used for persistence. References: `Engine.Core`, `Engine.Math`; Analyzer: `Engine.Generators`
- **`Modules.InMemoryParent`** — in-memory per-entity implementation of `IParent`. References: `Engine.Core`, `Engine.Hierarchy`; Analyzer: `Engine.Generators`

## Component Model

### Defining a Component

A component is defined by a **contract interface** that extends `IComponent<TData>`, where `TData` is the data type the component stores per entity:

```csharp
// in Engine.Math
public struct Pose { public Vector3 Position; public Quaternion Rotation; }
public interface IPose : IComponent<Pose> { }
```

### Implementing a Component

Concrete implementations extend `Component<TContract>` and must be marked `partial`. Each entity gets its own component instance — the owning entity is available via the `Entity` property. Implementations live in the `modules/` directory, separate from the contract definitions:

```csharp
// in Modules.InMemoryPose
public partial class InMemoryPose : Component<IPose>
{
    private Pose _data;

    public Task OnAddAsync(Pose initialData, CancellationToken ct = default) { ... }
    public Task OnRemoveAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
    public Task<Pose> GetAsync(CancellationToken ct = default) { ... }
    public Task SetAsync(Pose data, CancellationToken ct = default)
    {
        _data = data;
        RaiseUpdated(data);          // ← fire event after successful write
        return Task.CompletedTask;
    }
}
```

The source generator emits a partial class that:
- Adds `: IPose` to the class declaration
- Implements the `Updated` event
- Provides `RaiseUpdated(TData)` helper method

### One Implementation Per Contract

An entity can only have **one component implementation** per contract interface at a time. Adding a second implementation of the same contract (e.g., adding `DatabasePose` when `InMemoryPose` is already attached via `IPose`) throws `InvalidOperationException`.

### Component Lookup

Components can be looked up by **either** their contract interface or concrete type:

```csharp
await entity.GetComponentAsync<IPose, Pose>(ct);          // by contract → returns handle
await entity.GetComponentAsync<InMemoryPose, Pose>(ct);   // by concrete → returns handle
await entity.HasComponentAsync<IPose>(ct);                 // by contract → true
await entity.HasComponentAsync<InMemoryPose>(ct);          // by concrete → true
```

### Events

Component lifecycle events (`ComponentAdded`, `ComponentRemoved`) are fired by the `Entity` when components are added or removed. Data update events are **fired by the implementation** — implementations call `RaiseUpdated(data)` in `SetAsync` when the operation succeeds. The `ComponentHandle<TData>` subscribes directly to the component's `Updated` event and listens to the entity's `ComponentRemoved` event, exposing user-friendly `Updated` and `Removed` events.

## Extension Model

Extensions are class libraries that reference `Engine.Core` and `Engine.Generators` (as an analyzer). They implement `IExtension` to register their types. The runtime discovers extensions via assembly scanning at startup.

```
/app/extensions/
  ├── Acme.Physics.dll          # third-party physics behaviours
  └── MyGame.Combat.dll         # user's own behaviours
```

Each extension project should reference the generator so that client proxies and server stubs are generated at compile time:

```xml
<ProjectReference Include="..\Engine.Generators\Engine.Generators.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

From the runtime's perspective, all extensions are identical — there is no privileged "built-in" code. All domain logic is an extension.

## Key Concepts

| Concept       | Description |
|---------------|-------------|
| **Entity**    | A uniquely identified object in the world. Analogous to Unity's `GameObject`. Has no behaviour of its own; it is a container for Components. Implemented as a concrete `Entity` class with `EntityId`. |
| **Component** | Typed data attached to an Entity via `IComponent<TData>`. One implementation per contract interface. Each entity gets its own component instance via `Component<TContract>`. Implementations fire `Updated` events explicitly; lifecycle events (`ComponentAdded`/`ComponentRemoved`) are fired by Entity. |
| **Behaviour** | Logic that operates on Components. Each Behaviour is a remote service — its interface lives in Core, its proxy and server stub are generated by `Engine.Generators`, and its implementation lives in an extension. |
| **Plugin**    | A runtime lifecycle participant. Extends `Plugin` to receive `OnStartAsync`/`OnStopAsync` callbacks with a `World` reference. |
| **Extension** | A loadable module that registers Components, Behaviours, and Plugins with the runtime via `IExtension`. |

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

**Roslyn incremental source generators** in `Engine.Generators` scan each consuming compilation and emit:

1. **Client proxies** — for each `IBehaviour`-derived interface, a `{Name}Proxy` class is generated. Each method serializes its arguments with MessagePack, sends a NATS request to subject `behaviour.{TypeName}.{MethodName}`, and deserializes the response. Methods returning `Task` (void) use `PublishAsync`; methods returning `Task<T>` use `RequestAsync`. Multi-parameter methods are serialized as tuples.

2. **Server dispatch stubs** — for each concrete class implementing an `IBehaviour`-derived interface, a `{Name}Dispatcher` class is generated. The dispatcher subscribes to the corresponding NATS subjects, deserializes incoming payloads, invokes the implementation, serializes the result, and replies.

3. **MessagePack component resolver** — for each `IComponent` implementation, the type is registered in a generated `EngineComponentResolver` class that provides a pre-configured `MessagePackSerializerOptions` and a `ComponentTypes` list.

4. **Component partial classes** — for each `Component<TContract>` subclass, a partial class is generated that adds the contract interface, an `Updated` event, and a `RaiseUpdated` helper method. This separates the event plumbing from the implementation logic.

### Subject Naming Convention

Behaviour method subjects follow the pattern `behaviour.{TypeName}.{MethodName}` where:
- `TypeName` is the interface name with the leading `I` stripped (e.g., `IPhysics` → `Physics`).
- `MethodName` has the `Async` suffix stripped (e.g., `ApplyForceAsync` → `ApplyForce`).

### How to Use

Any project referencing `Engine.Generators` as an analyzer will automatically get generated code. Inspect generated output with:

```bash
dotnet build -p:EmitCompilerGeneratedFiles=true
# Generated files appear in obj/Debug/net9.0/generated/Engine.Generators/
```

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

| Package                          | Version | Used By    |
|----------------------------------|---------|------------|
| NATS.Net                         | 2.7.2   | Runtime    |
| MessagePack                      | 3.1.4   | Runtime    |
| Microsoft.CodeAnalysis.CSharp    | 4.12.0  | Generators |
| Microsoft.CodeAnalysis.Analyzers | 3.3.4   | Generators |
