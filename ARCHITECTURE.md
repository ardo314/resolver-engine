# Architecture

## Project Structure

Monorepo with engine packages under `engine/`, component definition modules under `modules/`, and worker implementations under `workers/`:

| Package                      | Path                | Description                                                  |
| ---------------------------- | ------------------- | ------------------------------------------------------------ |
| `@engine/core`               | `engine/core`       | Core types: entities, components                             |
| `@engine/backend`            | `engine/backend`    | Server-side entity structure management                      |
| `@engine/client`             | `engine/client`     | Client-side API                                              |
| `@engine/worker`             | `engine/worker`     | Worker system: workers, decorators, WorkerHost               |
| `@engine/editor`             | `engine/editor`     | Vite + React frontend                                        |
| `@ardo314/core`              | `modules/core`      | Core schemas and base components (pose, name, parent)        |
| `@ardo314/in-memory`         | `modules/in-memory` | In-memory component definitions that compose core components |
| `@ardo314/nova`              | `modules/nova`      | Nova component definitions that compose core components      |
| `@ardo314/in-memory-workers` | `workers/in-memory` | In-memory workers (depends on worker, in-memory)             |
| `@ardo314/nova-workers`      | `workers/nova`      | Nova workers (depends on worker, nova)                       |
| `@engine/nova-deploy`        | `deployments/nova`  | NOVA cell app installer and dev deployment tooling           |

All packages use TypeScript project references and build via `tsc --build`.

## Key Concepts

### Entity

A uniquely identifiable runtime object. Identified by an `EntityId` (branded string). Entities are created and managed by the `EntityRepository` on the backend.

### Method

A first-class standalone unit of behaviour, defined via `defineMethod(name, { input?, output? })`. Methods are namespaced (e.g. `"core.getPose"`, `"in-memory.setTarget"`). Each method has:

- **`name`** — A globally unique namespaced string identifier.
- **`input`** (optional) — A Zod schema describing the method's input type.
- **`output`** (optional) — A Zod schema describing the method's return type.

Methods carry a `__type: "method"` tag for runtime discrimination. The same `Method` object is reused across multiple component definitions.

### Component

A component is identified by a `ComponentId` (explicit branded string) and carries a list of methods. Defined via `defineComponent(id, [method1, method2, ...])`.

Components carry a `__type: "component"` tag for runtime discrimination.

**Identity:** Component IDs are explicit strings passed to `defineComponent`. Two components with the same ID are the same component.

**Constraints:**

- An entity can have at most one instance of a given component (by ID).
- Method names must be unique within a component. `defineComponent` throws at definition time if a duplicate is detected.

`ComponentReference<C>` infers a TypeScript interface from a component's method list, producing typed async method signatures keyed by method name.

### Query & Duck Typing

A `Query` is a list of methods, defined via `defineQuery([method1, method2, ...])`. Queries enable duck-typed matching across the **entire entity**: an entity matches a query if the union of methods across all its components covers every method in the query. Methods may be spread across different components.

When querying with `entity.query(query)`:

- The backend collects all methods from all components on the entity.
- If all requested methods are covered, the query matches and returns a `Record<methodName, componentId>` mapping.
- The client builds a `QueryReference<Q>` proxy where each method call routes to the correct component's NATS subject based on the mapping.

A component can also be used as a query since it carries a method list.

### Core Methods & Module Components

`@ardo314/core` defines standalone methods representing fundamental capabilities:

- `core.getPose` / `core.setPose` — position + rotation access
- `core.getName` / `core.setName` — display name access
- `core.getParent` / `core.setParent` — parent entity reference access

Core components (`core.pose`, `core.name`, `core.parent`) bundle these methods but have no workers of their own.

`@ardo314/in-memory` defines implementation-specific components that reuse the same core methods:

- `in-memory.name` uses `[core.getName, core.setName]`
- `in-memory.parent` uses `[core.getParent, core.setParent]`
- `in-memory.pose` uses `[core.getPose, core.setPose]`
- `in-memory.follow-pose` uses `[in-memory.getTarget, in-memory.setTarget, core.getPose, core.setPose]`

Because methods are shared across components, querying an entity with `defineQuery([getPose, setPose, getName])` will match if the entity has `in-memory.pose` (providing `getPose`/`setPose`) and `in-memory.name` (providing `getName`/`setName`). Workers implement the in-memory components.

### Component Worker

A `ComponentWorker` is a class that implements the runtime behaviour for a component. There is **one worker instance per component on an entity**. Workers are defined using a single decorator:

- **`@Implements(component)`** — Class decorator. Declares which component the worker implements. The decorator is generic over the component type: if the worker class does not implement all required methods, TypeScript reports a compile-time error. The expected shape is captured by the `WorkerImplementation<C>` type.

The component's method list is the single source of truth for which methods a worker must expose. For each method, the worker class provides a matching instance method (using the namespaced method name as the key, e.g. `"core.getPose"`). Method schemas come from the `defineMethod(...)` call — workers do not redeclare them.

Workers extend the abstract `ComponentWorker` base class. At `start()` time, the base class iterates over the component's methods to create per-method NATS subscriptions automatically. If a worker does not implement a required method, `start()` throws immediately (fail-fast).

**Worker lifecycle:** Workers run in separate containers (e.g. in Kubernetes), not inside the backend. Each worker module runs in its own container using a `WorkerHost`. On startup, the `WorkerHost` registers its components with the backend via `Subjects.registerComponent` (request/reply), sending the component's method names and schema. It then subscribes to `Subjects.startWorker` and `Subjects.stopWorker` (fire-and-forget publishes from the backend). When `startWorker` arrives with a matching `componentId`, the host instantiates the worker and calls `start(nc, entityId)`. When `stopWorker` arrives, it calls `stop()` and removes the instance.

Within a single worker instance, `start()` subscribes to per-method subjects for the component. `stop()` unsubscribes. Each method is identified by its name, its component, and its entity.

**Independence from backend:** Workers operate independently of the backend. The backend only tracks which entities have which components (structural data) and publishes lifecycle events. It does not relay or control worker subscriptions or method messages. Clients communicate with workers directly via `WorkerSubjects`.

## Serialization

Zod schemas serve as the single source of truth for both TypeScript types (via `z.infer`) and runtime validation.

### Component Schema Registration

When workers register components with the backend, they include a `ComponentSchema` — a JSON-serializable representation of the component's methods. Method schemas are converted from Zod types to JSON Schema using Zod v4's built-in `toJSONSchema()`. The `ComponentSchema` type is defined in `@engine/core`:

```typescript
interface ComponentSchema {
  methods: Record<string, { input?: JSONSchema; output?: JSONSchema }>;
}
```

The backend stores the schema alongside structural component data. Clients can retrieve schemas via `listComponents` and use method names from the schema when interacting with entities.

## Transport

Communication uses [NATS](https://nats.io/) request/reply and publish/subscribe with two subject namespaces:

- **`Subjects`** — Backend subjects for structural operations (entity/component management), queries, and worker lifecycle events. Handled by `EntityHandler`.
- **`WorkerSubjects`** — Per-component per-entity subjects for method calls. Handled directly by `ComponentWorker` instances.

Both are defined in `@engine/core`.

### Backend Subjects (structural)

| Subject                         | Payload (request)                  | Payload (reply)                         |
| ------------------------------- | ---------------------------------- | --------------------------------------- |
| `engine.world.createEntity`     | _(empty)_                          | `EntityId`                              |
| `engine.world.deleteEntity`     | `EntityId`                         | `"true"/"false"`                        |
| `engine.world.hasEntity`        | `EntityId`                         | `"true"/"false"`                        |
| `engine.world.listEntities`     | _(empty)_                          | `EntityId[]` (JSON)                     |
| `engine.entity.addComponent`    | `{ entityId, componentId }` (JSON) | `{ ok }` or `{ error }`                |
| `engine.entity.removeComponent` | `{ entityId, componentId }` (JSON) | `"true"/"false"`                        |
| `engine.entity.hasComponent`    | `{ entityId, componentId }` (JSON) | `"true"/"false"`                        |
| `engine.entity.getComponents`   | `EntityId`                         | `[{ componentId, methodNames }]` (JSON) |
| `engine.entity.query`           | `{ entityId, methodNames }` (JSON) | `{ match, methods? }` (JSON)            |

### Lifecycle Subjects

| Subject                     | Type          | Payload                                                | Description                                            |
| --------------------------- | ------------- | ------------------------------------------------------ | ------------------------------------------------------ |
| `engine.component.register` | Request/reply | `{ componentId, methodNames, schema }` → `{ ok }`      | Worker container registers a component with its schema |
| `engine.component.list`     | Request/reply | _(empty)_ → `[{ componentId, methodNames, schema }]`   | List all registered components with schemas            |
| `engine.worker.start`       | Publish       | `{ entityId, componentId }` (JSON)                     | Backend signals a worker should start                  |
| `engine.worker.stop`        | Publish       | `{ entityId, componentId }` (JSON)                     | Backend signals a worker should stop                   |

### Worker Subjects (per-component per-entity)

| Subject pattern                                          | Payload (request)  | Payload (reply)             |
| -------------------------------------------------------- | ------------------ | --------------------------- |
| `engine.worker.{componentId}.{entityId}.method.{method}` | `{ input }` (JSON) | `{ result }` or `{ error }` |

Each method gets its own NATS subject. Workers subscribe to these subjects on `start()` and unsubscribe on `stop()`.

## Build

- **Target:** ES2022
- **Module:** Node16
- **Build command:** `npm run build` (root)
- **Watch:** `npm run watch` (root)

## Deployment

Container images are built from Dockerfiles within the respective packages and deployed as NOVA cell apps via the `@engine/nova-deploy` package.

| Image                               | Dockerfile                     | Description                          |
| ----------------------------------- | ------------------------------ | ------------------------------------ |
| `component-engine-backend`          | `engine/backend/Dockerfile`    | Node.js server for entity management |
| `component-engine-editor`           | `engine/editor/Dockerfile`     | Vite/React SPA served via nginx      |
| `component-engine-in-memory-worker` | `workers/in-memory/Dockerfile` | In-memory worker host (Node.js)      |
| `component-engine-nova-worker`      | `workers/nova/Dockerfile`      | Nova worker host (Node.js)           |
| `component-engine-nova`             | `deployments/nova/Dockerfile`  | NOVA cell app installer (Node.js)    |

The backend image is a multi-stage Node.js build. The editor image builds the Vite SPA in a Node.js stage and serves the static output with nginx on port 8080, with SPA fallback routing.

### Local Development

Start each service in a separate terminal inside the devcontainer. VS Code auto-forwards the ports to the host browser.

```sh
npm run build                    # compile TypeScript (or npm run watch)
nats-server -c nats.conf         # start NATS
node engine/backend/dist/index.js          # start backend
node workers/in-memory/dist/index.js       # start in-memory worker host
cd engine/editor && npm run dev             # start Vite dev server
```

NATS listens on port 4222 (client), 8222 (monitoring), and 9222 (WebSocket). The browser editor connects to NATS via WebSocket on port 9222. `nats.conf` at the repo root configures NATS with HTTP monitoring and WebSocket. `engine/editor/.env` provides the `VITE_NATS_URL` so Vite serves the correct WebSocket URL to the browser.

### Graceful Shutdown

Backend and worker entry points register SIGTERM/SIGINT handlers that call `nc.drain()`. Drain is a NATS built-in that processes all in-flight messages, unsubscribes, and then closes the connection.

The `@engine/nova-deploy` package uses `@wandelbots/nova-api` to manage cell apps via the NOVA API and provides two entry points:

- **`install-apps`** (`node dist/install.js`) — Production mode. Runs inside a NOVA cell app container. Reads `NOVA_API`, `CELL_NAME`, `NATS_BROKER`, `BACKEND_IMAGE`, `EDITOR_IMAGE`, and `WORKER_IMAGES` (comma-delimited image URLs) from the environment, installs the backend, editor, and worker apps via `ApplicationApi.addApp()`, and stays alive.
- **`dev`** (`node dist/dev.js`) — Development mode. Runs locally. Builds TypeScript, builds and pushes Docker images with `:dev` tags, then deletes and reinstalls the apps in a NOVA cell. Supports `--skip-build`, `--backend-only`, and `--editor-only` flags.

## Versioning & Release

The project uses [semantic-release](https://github.com/semantic-release/semantic-release) for automated semantic versioning on `main`, following a trunk-based development workflow:

- **Branching model:** All work happens on short-lived feature branches. PRs are **squash-merged** into `main`, producing a single conventional commit per PR.
- **Commit convention:** [Conventional Commits](https://www.conventionalcommits.org/) — the squash merge commit message (PR title) determines the version bump: `feat:` → minor, `fix:`/`refactor:`/`perf:` → patch, `BREAKING CHANGE` → major.
- **Automation:** The `.github/workflows/release.yml` workflow runs on every push to `main`. After building, it invokes `semantic-release` which analyzes new commits, bumps the version, generates a changelog, creates a Git tag, and publishes a GitHub release.
- **Configuration:** `.releaserc.json` at the repo root. Plugins: commit-analyzer, release-notes-generator, changelog, npm (version update), git (commit back changelog + package.json), github (create release).
