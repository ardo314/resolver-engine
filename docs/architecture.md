# Resolver Engine — Architecture

## Overview

The Resolver Engine is a component-based entity system where lightweight `Entity` identifiers
are associated with **components** (data resolvers). Components are backed by pluggable
implementations (in-memory, networked via NATS, etc.) while presenting a uniform API to
consumers.

---

## Desired Developer API

### Creating entities and adding components

```csharp
var world = new World();

var entity = world.CreateEntity();

// Register a concrete component implementation.
// The component is queryable by both its concrete type AND any base component type.
entity.AddComponent<InMemoryParent>();
```

### Retrieving components

```csharp
// By abstract component type — returns the registered implementation
var parent = entity.GetComponent<Parent>();

// By concrete type — also works
var parent = entity.GetComponent<InMemoryParent>();
```

### Using components

```csharp
// Components expose Get/Set for their underlying data
var someEntity = world.CreateEntity();
parent.Set(someEntity);

var current = parent.Get(); // returns the Entity stored as parent
```

### Full usage example

```csharp
var world = new World();

var root   = world.CreateEntity();
var child  = world.CreateEntity();

child.AddComponent<InMemoryParent>();
child.AddComponent<InMemoryRigidTransform3D>();

// Set parent relationship
child.GetComponent<Parent>().Set(root);

// Set spatial transform
child.GetComponent<RigidTransform3D>().Set(
    new RigidTransform3DData(Vector3.UnitY, Quaternion.Identity)
);

// Query by concrete type works identically
var transform = child.GetComponent<InMemoryRigidTransform3D>();
var pos = transform.Get().Position;
```

---

## Core Concepts

### Entity

A lightweight identifier (GUID wrapper). Entities own nothing — all data lives in
components managed by the `World`.

```csharp
public readonly struct Entity : IEquatable<Entity>
{
    public Guid Id { get; }
}
```

### Component

A component is a named data slot on an entity. Every component has:

- An **abstract component type** (e.g. `Parent`, `RigidTransform3D`) that defines *what*
  data is stored.
- One or more **concrete implementations** (e.g. `InMemoryParent`, `NatsParent`) that
  define *how* it is stored or resolved.

#### Component type hierarchy

```
IComponent                          ← marker interface
  └─ IComponent<T>                  ← typed data: Get() / Set(T)
       └─ Parent : IComponent<Entity>         ← abstract component
            ├─ InMemoryParent                  ← stores in a field
            └─ NatsParent                      ← resolves over NATS (future)
       └─ RigidTransform3D : IComponent<RigidTransform3DData>
            └─ InMemoryRigidTransform3D
```

### World

The world is the top-level container. It owns entities and their component storage.

```csharp
public class World
{
    EntityRef CreateEntity();
    void DestroyEntity(Entity entity);
}
```

### EntityRef

A convenience handle returned by `World.CreateEntity()`. It binds an `Entity` id to its
`World` so the developer can call `AddComponent` / `GetComponent` directly.

```csharp
public readonly struct EntityRef
{
    public Entity Entity { get; }

    public T AddComponent<T>() where T : IComponent, new();
    public T GetComponent<T>() where T : class, IComponent;
    public bool HasComponent<T>() where T : class, IComponent;
    public bool RemoveComponent<T>() where T : class, IComponent;
}
```

---

## Proposed Type Inventory

### Engine.Core

| Type | Kind | Purpose |
|------|------|---------|
| `Entity` | struct | Lightweight entity identifier |
| `IComponent` | interface | Marker for all components |
| `IComponent<T>` | interface | Typed data component (`Get`/`Set`) |
| `World` | class | Entity & component storage |
| `EntityRef` | struct | Entity handle with component API |

### Modules.Core (abstract component types)

| Type | Resolves | Notes |
|------|----------|-------|
| `Parent` | `IComponent<Entity>` | Abstract parent relationship |

### Modules.Core.InMemory (concrete implementations)

| Type | Implements | Storage |
|------|-----------|---------|
| `InMemoryParent` | `Parent` | Field-backed |

### Modules.Spatial

| Type | Resolves | Notes |
|------|----------|-------|
| `RigidTransform3D` | `IComponent<RigidTransform3DData>` | Abstract spatial transform |

### Modules.Spatial.InMemory (future)

| Type | Implements | Storage |
|------|-----------|---------|
| `InMemoryRigidTransform3D` | `RigidTransform3D` | Field-backed |

---

## Component Registration & Lookup

When `AddComponent<InMemoryParent>()` is called, the world registers the instance under
**all resolvable types** in the component's hierarchy:

```
InMemoryParent  →  registers as: InMemoryParent, Parent, IComponent<Entity>, IComponent
```

This enables `GetComponent<Parent>()` to find an `InMemoryParent` instance, because
`InMemoryParent` was indexed under the `Parent` key.

Lookup order for `GetComponent<T>()`:

1. Exact type match → returns immediately.
2. Walk registered components, find first assignable to `T`.
3. No match → throws `ComponentNotFoundException` (or returns `null` via `TryGetComponent`).

---

## Naming Conventions

| Layer | Pattern | Example |
|-------|---------|---------|
| Abstract component | `{Name}` | `Parent`, `RigidTransform3D` |
| In-memory impl | `InMemory{Name}` | `InMemoryParent` |
| NATS-backed impl | `Nats{Name}` | `NatsParent` (future) |
| Data struct | `{Name}Data` | `RigidTransform3DData` |

The previous `IDataResolver<T>` → `IComponent<T>` and `I{Name}Resolver` → `{Name}`
renames keep the public API clean and intention-revealing.

---

## Migration from Current Code

| Current | New |
|---------|-----|
| `IDataResolver<T>` | `IComponent<T>` |
| `IParentResolver` | `Parent` (abstract class) |
| `InMemoryParentResolver` | `InMemoryParent` |
| `IRigidTransform3DResolver` | `RigidTransform3D` (abstract class) |
| — (no world) | `World` + `EntityRef` |

---

## Open Questions

1. **Thread safety** — Should `World` be thread-safe by default, or offer a
   `ConcurrentWorld` variant?
2. **Multiple components of the same abstract type** — Allow more than one `Parent` per
   entity? Current design assumes one-per-type.
3. **Component dependencies** — Should adding `InMemoryParent` automatically add required
   sibling components?
4. **Lifecycle hooks** — `OnAttach` / `OnDetach` callbacks when components are added/removed?
5. **Source generator integration** — The existing `[Generate]` attribute produces NATS
   client/server stubs. Should it also generate component boilerplate?
