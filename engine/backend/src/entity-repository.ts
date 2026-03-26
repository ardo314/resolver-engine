import type { EntityId } from "@engine/core";

export class EntityRepository {
  private readonly entities = new Set<EntityId>();
  private nextId = 0;

  create(): EntityId {
    const id = `${Date.now()}-${this.nextId++}` as EntityId;
    this.entities.add(id);
    return id;
  }

  destroy(id: EntityId): boolean {
    return this.entities.delete(id);
  }

  has(id: EntityId): boolean {
    return this.entities.has(id);
  }

  get all(): ReadonlySet<EntityId> {
    return this.entities;
  }
}
