# Architecture

## Project Structure

Monorepo with a C# engine, C# providers, a TypeScript client library, and a React editor.

### C# Projects (built via `dotnet build`)

| Project           | Path               | Type | Description                                                   |
| ----------------- | ------------------ | ---- | ------------------------------------------------------------- |
| `Engine`          | `engine/`          | Exe  | Core types + backend server (entity/component management)     |
| `Client`          | `clients/csharp/`  | Lib  | C# client library (World, Entity)                             |
| `Modules.Core`    | `modules/core/`    | Lib  | Core method and component definitions (pose, name, parent)    |
| `Modules.Nova`    | `modules/nova/`    | Lib  | Nova component definitions (reuses core methods)              |
| `Providers.Nova`  | `providers/nova/`  | Exe  | Nova provider — implements components (replaces TS workers)   |
| `Deployments.Nova`| `deployments/nova/`| Exe  | NOVA cell app installer                                       |

All C# projects target `net9.0`. The solution file is `ComponentEngine.sln`.

### TypeScript Packages (built via `tsc --build`)

| Package          | Path          | Description                                  |
| ---------------- | ------------- | -------------------------------------------- |
| `@engine/client` | `clients/js/` | Client library (core types + client API)     |
| `@engine/editor` | `editor/`     | Vite + React frontend                        |

TypeScript packages use project references and build via `npm run build` at the root.

## Key Concepts

### Entity

A uniquely identifiable runtime object. Identified by an `EntityId` (branded string). Entities are created and managed by the `EntityRepository` on the backend.

### Method

A first-class standalone unit of behaviour. In C#, defined via `Method.Define(name)` or its generic overloads (`Define<TOutput>`, `DefineWithInput<TInput>`, `Define<TInput, TOutput>`). In TypeScript, defined via `defineMethod(name, { input?, output? })` with Zod schemas. Methods are namespaced (e.g. `"getPose"`, `"setName"`). Each method has:

- **`name`** — A globally unique string identifier.
- **`input`** (optional) — The input type (C#: `Type`, TS: `z.ZodType`).
- **`output`** (optional) — The return type.

In TypeScript, methods carry a `__type: "method"` tag for runtime discrimination. The same `Method` object is reused across multiple component definitions.

### Component

A component is identified by a `ComponentId` and carries a list of methods. In C#, defined via `Component.Define(id, methods...)`. In TypeScript, defined via `defineComponent(id, [method1, method2, ...])`.

In TypeScript, components carry a `__type: "component"` tag for runtime discrimination.

**Identity:** Component IDs are explicit strings. Two components with the same ID are the same component.

**Constraints:**

- An entity can have at most one instance of a given component (by ID).
- Method names must be unique within a component. `Define`/`defineComponent` throws at definition time if a duplicate is detected.

In TypeScript, `ComponentReference<C>` infers a typed interface from a component's method list, producing typed async method signatures keyed by method name.

### Query & Duck Typing

A `Query` is a list of methods, defined via `defineQuery([method1, method2, ...])`. Queries enable duck-typed matching across the **entire entity**: an entity matches a query if the union of methods across all its components covers every method in the query. Methods may be spread across different components.

When querying with `entity.query(query)`:

- The backend collects all methods from all components on the entity.
- If all requested methods are covered, the query matches and returns a `Record<methodName, componentId>` mapping.
- The client builds a `QueryReference<Q>` proxy where each method call routes to the correct component's NATS subject based on the mapping.

A component can also be used as a query since it carries a method list.

### Core Methods & Module Components

`Modules.Core` (C#) / `@engine/client` (TS) defines standalone methods representing fundamental capabilities:

- `getPose` / `setPose` — position + rotation access (6-element double array: x, y, z, rx, ry, rz)
- `getName` / `setName` — display name access
- `getParent` / `setParent` — parent entity reference access

Core components (`core.pose`, `core.name`, `core.parent`) bundle these methods.

`Modules.Nova` (C#) defines Nova-specific components that reuse the same core methods:

- `nova.name` uses `[getName, setName]`
- `nova.parent` uses `[getParent, setParent]`
- `nova.pose` uses `[getPose, setPose]`

### Component Provider

A `ComponentProvider` (C#) is a class that implements the runtime behaviour for a component. There is **one provider instance per component on an entity**. Providers replace the previous TypeScript worker system.

Each provider extends `ComponentProvider` and implements `HandleMethod(methodName, input)` which dispatches method calls. The provider's `Component` property declares which component it implements.

**Provider lifecycle:** Providers run in separate containers (e.g. in Kubernetes), not inside the backend. Each provider module runs in its own container using a `ProviderHost`. On startup, the `ProviderHost` registers its components with the backend via `Subjects.RegisterComponent` (request/reply), sending the component's method names. It then subscribes to `Subjects.StartWorker` and `Subjects.StopWorker` (fire-and-forget publishes from the backend). When `StartWorker` arrives with a matching `componentId`, the host instantiates the provider and calls `Start(nc, entityId)`. When `StopWorker` arrives, it calls `Stop()` and removes the instance.

Within a single provider instance, `Start()` subscribes to per-method subjects for the component. `Stop()` unsubscribes. Each method is identified by its name, its component, and its entity.

**Independence from backend:** Providers operate independently of the backend. The backend only tracks which entities have which components (structural data) and publishes lifecycle events. It does not relay or control provider subscriptions or method messages. Clients communicate with providers directly via `WorkerSubjects`.

## Serialization

JSON is the wire format for all NATS messages. In C#, `System.Text.Json` handles serialization. In TypeScript, Zod schemas serve as the source of truth for types (via `z.infer`) and runtime validation.

### Component Schema Registration

When providers register components with the backend, they include a `ComponentSchema` — a JSON-serializable representation of the component's methods. The backend stores the schema alongside structural component data. Clients can retrieve schemas via `listComponents`.

## Transport

Communication uses [NATS](https://nats.io/) request/reply and publish/subscribe with two subject namespaces:

- **`Subjects`** — Backend subjects for structural operations (entity/component management), queries, and provider lifecycle events. Handled by `EntityHandler` (C#).
- **`WorkerSubjects`** — Per-component per-entity subjects for method calls. Handled directly by `ComponentProvider` instances (C#).

Both are defined in `Engine.Core` (C#) and `@engine/client` (TS).

### Backend Subjects (structural)

| Subject                         | Payload (request)                  | Payload (reply)                         |
| ------------------------------- | ---------------------------------- | --------------------------------------- |
| `engine.world.createEntity`     | _(empty)_                          | `EntityId`                              |
| `engine.world.deleteEntity`     | `EntityId`                         | `"true"/"false"`                        |
| `engine.world.hasEntity`        | `EntityId`                         | `"true"/"false"`                        |
| `engine.world.listEntities`     | _(empty)_                          | `EntityId[]` (JSON)                     |
| `engine.entity.addComponent`    | `{ entityId, componentId }` (JSON) | `{ ok }` or `{ error }`                 |
| `engine.entity.removeComponent` | `{ entityId, componentId }` (JSON) | `"true"/"false"`                        |
| `engine.entity.hasComponent`    | `{ entityId, componentId }` (JSON) | `"true"/"false"`                        |
| `engine.entity.getComponents`   | `EntityId`                         | `[{ componentId, methodNames }]` (JSON) |
| `engine.entity.query`           | `{ entityId, methodNames }` (JSON) | `{ match, methods? }` (JSON)            |

### Lifecycle Subjects

| Subject                     | Type          | Payload                                              | Description                                              |
| --------------------------- | ------------- | ---------------------------------------------------- | -------------------------------------------------------- |
| `engine.component.register` | Request/reply | `{ componentId, methodNames, schema }` → `{ ok }`    | Provider container registers a component with its schema |
| `engine.component.list`     | Request/reply | _(empty)_ → `[{ componentId, methodNames, schema }]` | List all registered components with schemas              |
| `engine.worker.start`       | Publish       | `{ entityId, componentId }` (JSON)                   | Backend signals a provider should start                  |
| `engine.worker.stop`        | Publish       | `{ entityId, componentId }` (JSON)                   | Backend signals a provider should stop                   |

### Worker Subjects (per-component per-entity)

| Subject pattern                                          | Payload (request)  | Payload (reply)             |
| -------------------------------------------------------- | ------------------ | --------------------------- |
| `engine.worker.{componentId}.{entityId}.method.{method}` | `{ input }` (JSON) | `{ result }` or `{ error }` |

Each method gets its own NATS subject. Providers subscribe to these subjects on `Start()` and unsubscribe on `Stop()`.

## Build

### C#

- **Framework:** .NET 9.0
- **Build command:** `dotnet build` (root)

### TypeScript

- **Target:** ES2022
- **Module:** ESNext (bundler resolution)
- **Build command:** `npm run build` (root)
- **Watch:** `npm run watch` (root)

## Deployment

Container images are built from Dockerfiles and deployed as NOVA cell apps via the `Deployments.Nova` project.

| Image                              | Dockerfile                    | Description                            |
| ---------------------------------- | ----------------------------- | -------------------------------------- |
| `component-engine-backend`         | `engine/Dockerfile`           | .NET backend for entity management     |
| `component-engine-editor`          | `editor/Dockerfile`           | Vite/React SPA served via nginx        |
| `component-engine-nova-provider`   | `providers/nova/Dockerfile`   | .NET Nova provider host                |
| `component-engine-nova`            | `deployments/nova/Dockerfile` | .NET NOVA cell app installer           |

The backend and provider images are multi-stage .NET builds. The editor image builds the Vite SPA in a Node.js stage and serves the static output with nginx on port 8080, with SPA fallback routing.

### Local Development

Start each service in a separate terminal inside the devcontainer. VS Code auto-forwards the ports to the host browser.

```sh
npm run build                        # compile TypeScript (or npm run watch)
dotnet build                         # compile C#
nats-server -c nats.conf             # start NATS
dotnet run --project engine          # start backend
dotnet run --project providers/nova  # start Nova provider
cd editor && npm run dev             # start Vite dev server
```

NATS listens on port 4222 (client), 8222 (monitoring), and 9222 (WebSocket). The browser editor connects to NATS via WebSocket on port 9222. `nats.conf` at the repo root configures NATS with HTTP monitoring and WebSocket. `editor/.env` provides the `VITE_NATS_URL` so Vite serves the correct WebSocket URL to the browser.

### Graceful Shutdown

Backend and provider entry points register SIGTERM/SIGINT handlers that cancel a `CancellationTokenSource`, causing subscriptions to end and the NATS connection to be disposed.

The `Deployments.Nova` project uses `HttpClient` to manage cell apps via the NOVA API:

- **`install`** (`dotnet run --project deployments/nova`) — Production mode. Runs inside a NOVA cell app container. Reads `NOVA_API`, `CELL_NAME`, `NATS_BROKER`, `BACKEND_IMAGE`, `EDITOR_IMAGE`, and `PROVIDER_IMAGE_N` (indexed provider image URLs) from the environment, installs apps via the NOVA Applications API, and stays alive.

## Versioning & Release

The project uses [semantic-release](https://github.com/semantic-release/semantic-release) for automated semantic versioning on `main`, following a trunk-based development workflow:

- **Branching model:** All work happens on short-lived feature branches. PRs are **squash-merged** into `main`, producing a single conventional commit per PR.
- **Commit convention:** [Conventional Commits](https://www.conventionalcommits.org/) — the squash merge commit message (PR title) determines the version bump: `feat:` → minor, `fix:`/`refactor:`/`perf:` → patch, `BREAKING CHANGE` → major.
- **Automation:** The `.github/workflows/release.yml` workflow runs on every push to `main`. After building, it invokes `semantic-release` which analyzes new commits, bumps the version, generates a changelog, creates a Git tag, and publishes a GitHub release.
- **Configuration:** `.releaserc.json` at the repo root. Plugins: commit-analyzer, release-notes-generator, changelog, npm (version update), git (commit back changelog + package.json), github (create release).
