# Architecture

## Project Structure

Monorepo with engine packages under `engine/` and user modules under `modules/`:

| Package                      | Path                        | Description                                                  |
| ---------------------------- | --------------------------- | ------------------------------------------------------------ |
| `@engine/core`               | `engine/core`               | Core types: entities, components                             |
| `@engine/backend`            | `engine/backend`            | Server-side entity structure management                      |
| `@engine/client`             | `engine/client`             | Client-side API                                              |
| `@engine/module`             | `engine/module`             | Module system: workers, decorators, WorkerHost               |
| `@engine/editor`             | `engine/editor`             | Vite + React frontend                                        |
| `@ardo314/core`              | `modules/core`              | Core schemas and base components (pose, name, parent)        |
| `@ardo314/in-memory`         | `modules/in-memory`         | In-memory component definitions that compose core components |
| `@ardo314/in-memory-workers` | `modules/in-memory-workers` | In-memory workers (depends on module, in-memory)             |
| `@ardo314/nova`              | `modules/nova`              | Nova component definitions that compose core components      |
| `@ardo314/nova-workers`      | `modules/nova-workers`      | Nova workers (depends on module, nova)                       |

All packages use TypeScript project references and build via `tsc --build`.

## Key Concepts

### Entity

A uniquely identifiable runtime object. Identified by an `EntityId` (branded string). Entities are created and managed by the `EntityRepository` on the backend.

### Component

A first-class data/behaviour unit identified by a `ComponentId` (explicit branded string). Defined via `defineComponent(id, { properties?, methods?, composites? })`.

- **`properties`** — `Record<string, z.ZodType>`. Each key is a property name; each value is a Zod schema describing the property's type.
- **`methods`** (optional) — `Record<string, { input?: z.ZodType; output?: z.ZodType }>`.
- **`composites`** (optional) — `readonly Component[]`. Other components that this component is composed of. Composition is recursive: a composite can itself compose other components.

Components carry a `__type: "component"` tag for runtime discrimination.

**Identity:** Component IDs are explicit strings passed to `defineComponent`. Two components with the same ID are the same component.

**Constraints:**

- An entity can have at most one instance of a given component (direct or via composition).
- Adding a component will fail if any of its composites (recursively) are already present on the entity, either as direct components or as composites of another component. This ensures no overlap.
- Property names must be unique across the entire component tree (own properties + all composite properties, recursively). `defineComponent` throws at definition time if a conflict is detected.

`ComponentReference<C>` infers a TypeScript interface from a component's definition, producing typed async get/set properties and typed method signatures. It is the intersection of the component's own properties/methods and all composite `ComponentReference`s.

### Component Composition & Query by Composite

Components can be composed of other components. For example, a `Transform` component might compose a `Pose` component and add additional properties.

When querying with `getComponent(component)`:

- If the component was added directly, the full reference (own + composite properties) is returned.
- If the component is a composite of a directly-added component, `hasComponent` returns `true` and a scoped reference containing only that composite's own properties is returned.

This allows code to query entities by capability without knowing the full composed component definition.

### Core Components & Module Components

`@ardo314/core` defines base components that represent fundamental capabilities:

- `core.pose` — position (vector) + rotation (quaternion)
- `core.name` — display name (string)
- `core.parent` — parent entity reference (EntityId)

These core components have no workers of their own. They exist as composites for module-specific components to include.

`@ardo314/in-memory` defines implementation-specific components that compose the core ones:

- `in-memory.name` composes `core.name`
- `in-memory.parent` composes `core.parent`
- `in-memory.pose` composes `core.pose`
- `in-memory.follow-pose` defines own `target` property and composes `core.pose`

Because in-memory components compose their core counterparts, querying an entity for `core.pose` will match any entity that has `in-memory.pose` or `in-memory.follow-pose` (or any other component that composes `core.pose`). Workers implement the in-memory components, not the core components directly.

### Component Worker

A `ComponentWorker` is a class that implements the runtime behaviour for a component. There is **one worker instance per component on an entity**. Workers are defined using a single decorator:

- **`@Implements(component)`** — Class decorator. Declares which component (defined via `defineComponent`) the worker implements. The single component carries its composites, so the worker implicitly covers everything. The decorator is generic over the component type: if the worker class does not implement all required property accessors and methods, TypeScript reports a compile-time error. The expected shape is captured by the `WorkerImplementation<C>` type.

The component definition is the single source of truth for which properties and methods a worker exposes. For each component property, the worker class provides a matching field implementing the `ComponentProperty<T>` interface — an object with `get()` and `set(value)` methods (sync or async). For each component method, the worker provides a matching instance method. Property schemas and method signatures come from the component's `defineComponent(...)` call — workers do not redeclare them.

Workers extend the abstract `ComponentWorker` base class. At `start()` time, the base class reads `getAllProperties(component)` and `getAllMethods(component)` from the component definition to create per-property and per-method NATS subscriptions automatically. If a worker does not implement the required `get`/`set` accessor for a property, or a required method, `start()` throws immediately (fail-fast).

**Worker lifecycle:** Workers run in separate containers (e.g. in Kubernetes), not inside the backend. Each worker module runs in its own container using a `WorkerHost`. On startup, the `WorkerHost` registers its components with the backend via `Subjects.registerComponent` (request/reply). It then subscribes to `Subjects.startWorker` and `Subjects.stopWorker` (fire-and-forget publishes from the backend). When `startWorker` arrives with a matching `componentId`, the host instantiates the worker and calls `start(nc, entityId)`. When `stopWorker` arrives, it calls `stop()` and removes the instance.

Within a single worker instance, `start()` subscribes to per-property `get`/`set` subjects and per-method subjects for the component and all its composites. `stop()` unsubscribes. Each property and method is identified by its name, its component, and its entity.

**Independence from backend:** Workers operate independently of the backend. The backend only tracks which entities have which components (structural data) and publishes lifecycle events. It does not relay or control worker subscriptions, property messages, or method messages. Clients communicate with workers directly via `WorkerSubjects`.

Worker classes are registered with the `WorkerHost` at container startup. When the backend adds a component, it records the structure, publishes a `startWorker` event, and the appropriate worker container handles it. When removed or when an entity is deleted, the backend publishes `stopWorker` events. A composed component is implemented by a single worker that covers all properties (own + composites).

## Serialization

Zod schemas serve as the single source of truth for both TypeScript types (via `z.infer`) and runtime validation.

## Transport

Communication uses [NATS](https://nats.io/) request/reply and publish/subscribe with three subject namespaces:

- **`Subjects`** — Backend subjects for structural operations (entity/component management) and worker lifecycle events. Handled by `EntityHandler`.
- **`WorkerSubjects`** — Per-component per-entity subjects for property access and method calls. Handled directly by `ComponentWorker` instances.

Both are defined in `@engine/core`.

### Backend Subjects (structural)

| Subject                         | Payload (request)                  | Payload (reply)                            |
| ------------------------------- | ---------------------------------- | ------------------------------------------ |
| `engine.world.createEntity`     | _(empty)_                          | `EntityId`                                 |
| `engine.world.deleteEntity`     | `EntityId`                         | `"true"/"false"`                           |
| `engine.world.hasEntity`        | `EntityId`                         | `"true"/"false"`                           |
| `engine.world.listEntities`     | _(empty)_                          | `EntityId[]` (JSON)                        |
| `engine.entity.addComponent`    | `{ entityId, componentId }` (JSON) | `{ ok }` or `{ error }`                    |
| `engine.entity.removeComponent` | `{ entityId, componentId }` (JSON) | `"true"/"false"`                           |
| `engine.entity.hasComponent`    | `{ entityId, componentId }` (JSON) | `"true"/"false"`                           |
| `engine.entity.getComponents`   | `EntityId`                         | `[{ componentId }]` (JSON, structure only) |

### Lifecycle Subjects

| Subject                     | Type          | Payload                                    | Description                            |
| --------------------------- | ------------- | ------------------------------------------ | -------------------------------------- |
| `engine.component.register` | Request/reply | `{ componentId, compositeIds }` → `{ ok }` | Worker container registers a component |
| `engine.worker.start`       | Publish       | `{ entityId, componentId }` (JSON)         | Backend signals a worker should start  |
| `engine.worker.stop`        | Publish       | `{ entityId, componentId }` (JSON)         | Backend signals a worker should stop   |

### Worker Subjects (per-component per-entity)

| Subject pattern                                                  | Payload (request)  | Payload (reply)             |
| ---------------------------------------------------------------- | ------------------ | --------------------------- |
| `engine.worker.{componentId}.{entityId}.property.{property}.get` | _(empty)_          | `{ value }` or `{ error }`  |
| `engine.worker.{componentId}.{entityId}.property.{property}.set` | `{ value }` (JSON) | `{ ok }` or `{ error }`     |
| `engine.worker.{componentId}.{entityId}.method.{method}`         | `{ input }` (JSON) | `{ result }` or `{ error }` |

Each property and method gets its own NATS subject. Workers subscribe to these subjects on `start()` and unsubscribe on `stop()`.

## Build

- **Target:** ES2022
- **Module:** Node16
- **Build command:** `npm run build` (root)
- **Watch:** `npm run watch` (root)
