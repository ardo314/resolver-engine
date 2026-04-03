# Architecture

## Project Structure

Monorepo with engine packages under `engine/` and user modules under `modules/`:

| Package                      | Path                        | Description                                          |
| ---------------------------- | --------------------------- | ---------------------------------------------------- |
| `@engine/core`               | `engine/core`               | Core types: entities, components                     |
| `@engine/backend`            | `engine/backend`            | Server-side entity management                        |
| `@engine/client`             | `engine/client`             | Client-side API                                      |
| `@engine/module`             | `engine/module`             | Module system (depends on client)                    |
| `@engine/editor`             | `engine/editor`             | Vite + React frontend                                |
| `@ardo314/core`              | `modules/core`              | Core user Zod schemas (depends on core)              |
| `@ardo314/in-memory`         | `modules/in-memory`         | User module: component definitions (depends on core) |
| `@ardo314/in-memory-workers` | `modules/in-memory-workers` | User module: workers (depends on module, in-memory)  |

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

### Component Worker

A `ComponentWorker` is a class that implements the runtime behaviour for a component. Workers are defined using two decorators:

- **`@Implements(component)`** — Class decorator. Declares which component (defined via `defineComponent`) the worker implements. The single component carries its composites, so the worker implicitly covers everything.
- **`@SerializeField(zodSchema)`** — Field decorator. Marks a class field as a serializable property backed by a Zod schema. The field name must match the corresponding component property name. The Zod schema is used for runtime validation on `set`.

Workers extend the abstract `ComponentWorker` base class. The engine instantiates workers via `new WorkerClass()` when a component is added to an entity. The decorator metadata (via TC39 `Symbol.metadata`) is used to auto-generate async `get`/`set` accessors that bridge plain class fields to the accessor interface expected by the `EntityHandler`.

Worker classes are registered with the `EntityHandler` on the backend at startup. A composed component is implemented by a single worker that covers all properties (own + composites).

> **Zod schemas vs component definitions:** Zod schemas (`z.string()`, `poseSchema`, etc.) describe data shapes for validation. Component definitions (`defineComponent(...)`) are first-class contracts with an ID, properties, methods, and composites. `@SerializeField` takes a Zod schema; `@Implements` takes a component.

## Serialization

Zod schemas serve as the single source of truth for both TypeScript types (via `z.infer`) and runtime validation.

## Transport

Client ↔ Backend communication uses [NATS](https://nats.io/) request/reply.

- **Subject conventions** are defined in `@engine/core` (`Subjects`).
- **Client** (`World`, `Entity`) sends NATS requests via `NatsConnection.request()`.
- **Backend** (`EntityHandler`) subscribes to subjects and delegates to `EntityRepository`.

### NATS Subjects

| Subject                         | Payload (request)                                   | Payload (reply)            |
| ------------------------------- | --------------------------------------------------- | -------------------------- |
| `engine.world.createEntity`     | _(empty)_                                           | `EntityId`                 |
| `engine.world.deleteEntity`     | `EntityId`                                          | `"true"/"false"`           |
| `engine.world.hasEntity`        | `EntityId`                                          | `"true"/"false"`           |
| `engine.entity.addComponent`    | `{ entityId, componentId }` (JSON)                  | `{ ok }` or `{ error }`    |
| `engine.entity.removeComponent` | `{ entityId, componentId }` (JSON)                  | `"true"/"false"`           |
| `engine.entity.hasComponent`    | `{ entityId, componentId }` (JSON)                  | `"true"/"false"`           |
| `engine.entity.getProperty`     | `{ entityId, componentId, property }` (JSON)        | `{ value }` or `{ error }` |
| `engine.entity.setProperty`     | `{ entityId, componentId, property, value }` (JSON) | `{ ok }` or `{ error }`    |

## Build

- **Target:** ES2022
- **Module:** Node16
- **Build command:** `npm run build` (root)
- **Watch:** `npm run watch` (root)
