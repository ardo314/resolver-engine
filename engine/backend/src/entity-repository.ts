import type { EntityId, ComponentId } from "@engine/core";

export class EntityRepository {
  private readonly entities = new Set<EntityId>();
  private nextId = 0;

  /** entityId → set of direct componentIds */
  private readonly components = new Map<EntityId, Set<ComponentId>>();

  create(): EntityId {
    const id = `${Date.now()}-${this.nextId++}` as EntityId;
    this.entities.add(id);
    this.components.set(id, new Set());
    return id;
  }

  delete(id: EntityId): boolean {
    this.components.delete(id);
    return this.entities.delete(id);
  }

  has(id: EntityId): boolean {
    return this.entities.has(id);
  }

  getAll(): ReadonlySet<EntityId> {
    return this.entities;
  }

  addComponent(entityId: EntityId, componentId: ComponentId): void {
    const entityComponents = this.components.get(entityId);
    if (!entityComponents) {
      throw new Error(`Entity ${entityId as string} does not exist`);
    }
    if (entityComponents.has(componentId)) {
      throw new Error(
        `Component ${componentId as string} already exists on entity ${entityId as string}`,
      );
    }
    entityComponents.add(componentId);
  }

  removeComponent(entityId: EntityId, componentId: ComponentId): boolean {
    const entityComponents = this.components.get(entityId);
    if (!entityComponents) return false;
    return entityComponents.delete(componentId);
  }

  hasComponent(entityId: EntityId, componentId: ComponentId): boolean {
    return this.components.get(entityId)?.has(componentId) ?? false;
  }

  getComponentIds(entityId: EntityId): ComponentId[] {
    const set = this.components.get(entityId);
    return set ? [...set] : [];
  }
}
