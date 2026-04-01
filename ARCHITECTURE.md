# Architecture

## Project Structure

Monorepo with engine packages under `engine/` and user modules under `modules/`:

| Package                      | Path                        | Description                                |
| ---------------------------- | --------------------------- | ------------------------------------------ |
| `@engine/core`               | `engine/core`               | Core types: entities, schemas, components  |
| `@engine/backend`            | `engine/backend`            | Server-side entity management              |
| `@engine/client`             | `engine/client`             | Client-side API                            |
| `@engine/module`             | `engine/module`             | Module system (depends on client)          |
| `@engine/editor`             | `engine/editor`             | Vite + React frontend                      |
| `@ardo314/core`              | `modules/core`              | Core user schemas (depends on core)        |
| `@ardo314/in-memory`         | `modules/in-memory`         | User module (depends on core)              |
| `@ardo314/in-memory-workers` | `modules/in-memory-workers` | User module (depends on module, in-memory) |

All packages use TypeScript project references and build via `tsc --build`.

## Key Concepts

### Entity

A uniquely identifiable runtime object. Identified by an `EntityId` (branded string). Entities are created and managed by the `EntityRepository` on the backend.

### Schema

A first-class data/behaviour contract identified by a `SchemaId` (explicit branded string). Defined via `defineSchema(id, { properties?, methods? })`.

- **`properties`** — `Record<string, z.ZodType>`. Each key is a property name; each value is a zod schema describing the property's type.
- **`methods`** (optional) — `Record<string, { input: z.ZodType; output?: z.ZodType }>`.

Schemas carry a `__type: "schema"` tag for runtime discrimination.

`SchemaReference<S>` infers a TypeScript interface from a schema's definition, producing typed async get/set properties and typed method signatures.

### Component

A composition of one or more schemas. Defined via `defineComponent(...schemas)`.

- **Identity** is derived deterministically from the sorted schema IDs joined by `|`. There is no separate component name — two components with the same set of schemas are considered identical.
- An entity can have at most one component of a given type.
- Schema overlap across different components on the same entity is disallowed; `addComponent` will fail if any of the component's schemas are already provided by an existing component.
- Components carry a `__type: "component"` tag for runtime discrimination.

`ComponentReference<C>` is the intersection of all `SchemaReference<S>` for each schema in the component.

### Duck-typing by Schema

Entities can be duck-typed by schema. `getComponent` accepts either a `Component` (returns `ComponentReference`) or a `Schema` (returns `SchemaReference` scoped to only that schema's properties/methods). This allows code to query entities by capability without knowing the full component definition.

### Component Worker

A `ComponentWorker` is a class that implements the runtime behaviour for a component's schemas. Workers are defined using two decorators:

- **`@Implements(...schemas)`** — Class decorator. Declares which engine schemas (defined via `defineSchema`) the worker satisfies. The component identity is derived from these schemas at registration time (same deterministic ID as `defineComponent`).
- **`@SerializeField(zodSchema)`** — Field decorator. Marks a class field as a serializable property backed by a Zod schema. The field name must match the corresponding schema property name. The Zod schema is used for runtime validation on `set`.

Workers extend the abstract `ComponentWorker` base class. The engine instantiates workers via `new WorkerClass()` when a component is added to an entity. The decorator metadata (via TC39 `Symbol.metadata`) is used to auto-generate async `get`/`set` accessors that bridge plain class fields to the accessor interface expected by the `EntityHandler`.

Worker classes are registered with the `EntityHandler` on the backend at startup.

> **Zod schemas vs engine schemas:** Zod schemas (`z.string()`, `poseSchema`, etc.) describe data shapes for validation. Engine schemas (`defineSchema(...)`) are first-class contracts with an ID, properties, and methods. `@SerializeField` takes a Zod schema; `@Implements` takes engine schemas.

## Serialization

Zod schemas serve as the single source of truth for both TypeScript types (via `z.infer`) and runtime validation.

## Transport

Client ↔ Backend communication uses [NATS](https://nats.io/) request/reply.

- **Subject conventions** are defined in `@engine/core` (`Subjects`).
- **Client** (`World`, `Entity`) sends NATS requests via `NatsConnection.request()`.
- **Backend** (`EntityHandler`) subscribes to subjects and delegates to `EntityRepository`.

### NATS Subjects

| Subject                         | Payload (request)                                | Payload (reply)            |
| ------------------------------- | ------------------------------------------------ | -------------------------- |
| `engine.world.createEntity`     | _(empty)_                                        | `EntityId`                 |
| `engine.world.deleteEntity`     | `EntityId`                                       | `"true"/"false"`           |
| `engine.world.hasEntity`        | `EntityId`                                       | `"true"/"false"`           |
| `engine.entity.addComponent`    | `{ entityId, componentId }` (JSON)               | `{ ok }` or `{ error }`    |
| `engine.entity.removeComponent` | `{ entityId, componentId }` (JSON)               | `"true"/"false"`           |
| `engine.entity.hasComponent`    | `{ entityId, componentId }` (JSON)               | `"true"/"false"`           |
| `engine.entity.getProperty`     | `{ entityId, schemaId, property }` (JSON)        | `{ value }` or `{ error }` |
| `engine.entity.setProperty`     | `{ entityId, schemaId, property, value }` (JSON) | `{ ok }` or `{ error }`    |

## Build

- **Target:** ES2022
- **Module:** Node16
- **Build command:** `npm run build` (root)
- **Watch:** `npm run watch` (root)
