# Architecture

## Overview

Resolver Engine is an **Entity-Component-Behaviour** engine built with Node.js and TypeScript. Entities are lightweight identifiers; behaviours define data contracts as interfaces; component markers declare which behaviours they provide; module workers provide concrete storage and logic for those contracts. Communication between the backend and module runtimes uses **NATS** as the message transport with **MessagePack** serialization. Unlike the previous .NET version that used Roslyn source generators, the Node.js version uses explicit dispatch methods on worker classes to route behaviour method calls.

## Solution Layout

```
component-engine/
├── package.json                      # Root workspace config (npm workspaces)
├── tsconfig.json                     # Base TypeScript config
├── src/                              # Core libraries and executables
│   ├── engine-core                   # Shared value types (EntityId)
│   ├── engine-client                 # Client-side proxies (Entity, World) and contracts (IComponent, IBehaviour, value types)
│   ├── engine-worker                 # Worker-side abstractions (ComponentWorker, WorkerRegistration)
│   ├── engine-backend                # Central backend process
│   └── engine-worker-runtime         # Host for running workers
└── modules/                          # Pluggable module implementations
    ├── in-memory                     # Component marker definitions (InMemoryPose, InMemoryParent)
    └── in-memory-workers             # In-memory component workers (IPose, IParent)
```

## Build & Tooling

| Setting            | Value                              |
| ------------------ | ---------------------------------- |
| Runtime            | Node.js 22+                        |
| Language           | TypeScript 5.7+ (strict mode)      |
| Module system      | Node16 (ESM with `.js` extensions) |
| Package management | npm workspaces                     |
| Serialization      | MessagePack (`@msgpack/msgpack`)   |
| Transport          | NATS (`nats` npm package)          |

### Package Dependency Versions

| Package            | Version | Used by                                                 |
| ------------------ | ------- | ------------------------------------------------------- |
| `@msgpack/msgpack` | ^3.0.0  | engine-client, engine-worker-runtime, in-memory-workers |
| `nats`             | ^2.28.0 | engine-client, engine-backend, engine-worker-runtime    |
| `typescript`       | ^5.7.0  | Root dev dependency                                     |
| `@types/node`      | ^22.0.0 | Root dev dependency                                     |

### NPM Scripts

- **build** — `tsc --build` (incremental project references build)
- **clean** — `tsc --build --clean`
- **watch** — `tsc --build --watch`

### Docker

Engine.Backend ships with a Dockerfile at `src/engine-backend/Dockerfile`. Build the image from the repository root:

```bash
docker build -f src/engine-backend/Dockerfile -t engine-backend .
```

The image uses `node:22-slim` as the base and runs the compiled backend.

## Key Concepts

### Entity

An entity is a lightweight identity represented by `EntityId` (a class wrapping a UUID string). The `Entity` class in engine-client associates an `EntityId` with methods to add, remove, query, and retrieve components via NATS request-reply.

### EntityRepository

`EntityRepository` (engine-backend) is the central in-memory store for entity existence and per-entity component sets. Used by `EntityService`, providing a single source of truth for:

- Entity lifecycle — `create`, `destroy`, `exists`, `listAll`.
- Component tracking — `addComponent`, `removeComponent`, `hasComponent`, `listComponents`.

### Component

A **component** is a named marker object that declares which behaviours it provides.

Components represent a named, deployable unit of functionality. They carry no data or logic themselves — they serve as identifiers for adding/removing functionality to entities.

Component markers, behaviour interfaces, and all contracts live in **engine-client** so they can be shared between client and module code without requiring a dependency on the backend.

### Behaviour

A **behaviour** is a data/logic contract defined as a TypeScript interface in engine-client.

- `IBehaviour` — marker interface; all behaviours extend this.
- `IDataBehaviour<T>` — a convenience base for behaviours that hold typed data with async `getDataAsync` and `setDataAsync` methods.
- `IPose` — position and rotation (Vector3 + Quaternion).
- `IParent` — parent-child entity relationship (EntityId string).

Behaviours are **interfaces only**; they carry no implementation.

### Component Markers

Each module defines **component marker objects** in a core package (e.g. `in-memory`) that declare the component name and which behaviour interfaces it provides:

```typescript
export const InMemoryPose = {
  componentName: "InMemoryPose",
  behaviourNames: ["IPose"],
} as const;
```

### ComponentWorker

`ComponentWorker` (engine-worker) is the abstract base class for module workers. It provides:

- `entityId` property — set by the runtime after construction to identify which entity this worker belongs to.
- `componentName` — abstract property declaring the component name.
- `behaviourNames` — abstract property declaring which behaviours this worker provides.
- `onAdded()` — called when the component is attached to an entity.
- `onRemoved()` — called when the component is removed.
- `dispatch(behaviourName, methodName, payload)` — abstract method for routing method calls. Replaces the Roslyn-generated `IDataDispatch` from the .NET version.

Concrete workers (e.g., `InMemoryPoseWorker`, `InMemoryParentWorker`) extend this base and implement the `dispatch` method with a switch over method names. One worker instance is created per (EntityId, componentName) pair.

### WorkerRegistration

`WorkerRegistration` (engine-worker) is a plain object that registers a component worker type:

```typescript
interface WorkerRegistration {
  componentName: string;
  behaviourNames: string[];
  create: () => ComponentWorker;
}
```

### WorkerRuntime

`WorkerRuntime` (engine-worker-runtime) hosts worker instances and manages NATS subscriptions for worker lifecycle (`worker.create.*`, `worker.remove.*`) and component method dispatch (`component.*.*`). Module executables call `startWorkerRuntime(registrations)` to connect to NATS and begin processing.

### World

`World` (engine-client) is the client-side proxy to the backend `EntityService`. It accepts a NATS connection and forwards entity lifecycle operations over NATS request-reply:

- `createEntityAsync` → `entity.create`
- `destroyEntityAsync` → `entity.destroy`
- `entityExistsAsync` → `entity.exists`
- `listEntitiesAsync` → `entity.list`

## Project Dependency Graph

```
engine-core  (no dependencies — contains EntityId only)
    ↑
engine-client  ── packages ──▶ nats, @msgpack/msgpack
    ↑
engine-worker  (depends on engine-core)
    ↑
engine-backend  ── packages ──▶ nats
    │
engine-worker-runtime  ── packages ──▶ nats, @msgpack/msgpack
    ↑
in-memory  (depends on engine-client — component markers)
    ↑
in-memory-workers  ── packages ──▶ @msgpack/msgpack
    (depends on engine-worker, engine-worker-runtime, in-memory)
```

## NATS Subject Conventions

| Subject Pattern                          | Direction               | Purpose                           |
| ---------------------------------------- | ----------------------- | --------------------------------- |
| `entity.create`                          | Client → Backend        | Create a new entity               |
| `entity.destroy`                         | Client → Backend        | Destroy an entity                 |
| `entity.exists`                          | Client → Backend        | Check entity existence            |
| `entity.list`                            | Client → Backend        | List all entities                 |
| `entity.add-component`                   | Client → Backend        | Add a component to an entity      |
| `entity.remove-component`                | Client → Backend        | Remove a component from an entity |
| `entity.has-component`                   | Client → Backend        | Check if entity has a component   |
| `entity.list-components`                 | Client → Backend        | List components on an entity      |
| `worker.create.<componentName>`          | Backend → WorkerRuntime | Create a worker instance          |
| `worker.remove.<componentName>`          | Backend → WorkerRuntime | Remove a worker instance          |
| `component.<behaviourName>.<methodName>` | Client → WorkerRuntime  | Invoke a behaviour method         |
