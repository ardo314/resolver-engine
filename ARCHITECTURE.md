# Architecture

## Project Structure

Monorepo with three packages under `engine/`:

| Package           | Path             | Description                                 |
| ----------------- | ---------------- | ------------------------------------------- |
| `@engine/core`    | `engine/core`    | Core types: entities, components, contracts |
| `@engine/backend` | `engine/backend` | Server-side entity management               |
| `@engine/client`  | `engine/client`  | Client-side API                             |
| `@engine/module`  | `engine/module`  | Module system (depends on client)           |
| `@engine/editor`  | `engine/editor`  | Vite + React frontend                       |

All packages use TypeScript project references and build via `tsc --build`.

## Key Concepts

### Entity

A uniquely identifiable runtime object. Identified by an `EntityId` (branded string). Entities are created and managed by the `EntityRepository` on the backend.

### Component

A schema-defined data and behaviour contract that can be attached to entities. Defined via `defineComponent(id, contract)`.

### Component Contract

A `ComponentContract` declares a component's shape using [zod](https://zod.dev/) schemas:

- **`properties`** — `Record<string, z.ZodType>`. Each key is a property name; each value is a zod schema describing the property's type.
- **`methods`** (optional) — `Record<string, { input: z.ZodType; output?: z.ZodType }>`. Each method has exactly one `input` schema. The `output` schema is optional; when omitted the method returns `void`.

`ComponentProxy<T>` infers a TypeScript interface from a component's contract, producing typed properties and typed method signatures.

## Serialization

Zod schemas serve as the single source of truth for both TypeScript types (via `z.infer`) and runtime validation.

## Transport

Client ↔ Backend communication uses [NATS](https://nats.io/) request/reply.

- **Subject conventions** are defined in `@engine/core` (`Subjects`).
- **Client** (`World`, `Entity`) sends NATS requests via `NatsConnection.request()`.
- **Backend** (`WorldHandler`) subscribes to subjects and delegates to `EntityRepository`.

### NATS Subjects

| Subject                         | Payload (request)                  | Payload (reply)  |
| ------------------------------- | ---------------------------------- | ---------------- |
| `engine.world.createEntity`     | _(empty)_                          | `EntityId`       |
| `engine.world.deleteEntity`     | `EntityId`                         | `"true"/"false"` |
| `engine.world.hasEntity`        | `EntityId`                         | `"true"/"false"` |
| `engine.entity.addComponent`    | `{ entityId, componentId }` (JSON) | _(empty)_        |
| `engine.entity.removeComponent` | `{ entityId, componentId }` (JSON) | _(empty)_        |
| `engine.entity.hasComponent`    | `{ entityId, componentId }` (JSON) | `"true"/"false"` |

## Build

- **Target:** ES2022
- **Module:** Node16
- **Build command:** `npm run build` (root)
- **Watch:** `npm run watch` (root)
