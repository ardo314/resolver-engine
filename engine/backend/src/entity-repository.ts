import type { EntityId, ComponentId } from "@engine/core";

export class EntityRepository {
  private readonly entities = new Set<EntityId>();
  private nextId = 0;

  /** entityId → componentId → worker proxy instance */
  private readonly components = new Map<EntityId, Map<ComponentId, unknown>>();

  /** entityId → compositeComponentId → composed componentId (for composite lookup) */
  private readonly compositeIndex = new Map<
    EntityId,
    Map<ComponentId, ComponentId>
  >();

  create(): EntityId {
    const id = `${Date.now()}-${this.nextId++}` as EntityId;
    this.entities.add(id);
    this.components.set(id, new Map());
    this.compositeIndex.set(id, new Map());
    return id;
  }

  delete(id: EntityId): boolean {
    this.components.delete(id);
    this.compositeIndex.delete(id);
    return this.entities.delete(id);
  }

  has(id: EntityId): boolean {
    return this.entities.has(id);
  }

  getAll(): ReadonlySet<EntityId> {
    return this.entities;
  }

  addComponent(
    entityId: EntityId,
    componentId: ComponentId,
    compositeIds: ComponentId[],
    workerInstance: unknown,
  ): void {
    const entityComponents = this.components.get(entityId);
    const entityComposites = this.compositeIndex.get(entityId);
    if (!entityComponents || !entityComposites) {
      throw new Error(`Entity ${entityId as string} does not exist`);
    }
    if (entityComponents.has(componentId)) {
      throw new Error(
        `Component ${componentId as string} already exists on entity ${entityId as string}`,
      );
    }
    // Check if the component is already provided as a composite of another component
    if (entityComposites.has(componentId)) {
      const existing = entityComposites.get(componentId)!;
      throw new Error(
        `Component ${componentId as string} is already provided as a composite of ${existing as string} on entity ${entityId as string}`,
      );
    }
    // Check that none of the composites are already present (as direct or composite)
    for (const compositeId of compositeIds) {
      if (entityComponents.has(compositeId)) {
        throw new Error(
          `Composite ${compositeId as string} is already a direct component on entity ${entityId as string}`,
        );
      }
      if (entityComposites.has(compositeId)) {
        const existing = entityComposites.get(compositeId)!;
        throw new Error(
          `Composite ${compositeId as string} is already provided by component ${existing as string} on entity ${entityId as string}`,
        );
      }
    }
    entityComponents.set(componentId, workerInstance);
    for (const compositeId of compositeIds) {
      entityComposites.set(compositeId, componentId);
    }
  }

  removeComponent(
    entityId: EntityId,
    componentId: ComponentId,
    compositeIds: ComponentId[],
  ): boolean {
    const entityComponents = this.components.get(entityId);
    const entityComposites = this.compositeIndex.get(entityId);
    if (!entityComponents || !entityComposites) return false;
    if (!entityComponents.delete(componentId)) return false;
    for (const compositeId of compositeIds) {
      entityComposites.delete(compositeId);
    }
    return true;
  }

  hasComponent(entityId: EntityId, componentId: ComponentId): boolean {
    const direct = this.components.get(entityId)?.has(componentId) ?? false;
    if (direct) return true;
    return this.compositeIndex.get(entityId)?.has(componentId) ?? false;
  }

  getWorkerInstance(
    entityId: EntityId,
    componentId: ComponentId,
  ): unknown | undefined {
    // Direct component
    const direct = this.components.get(entityId)?.get(componentId);
    if (direct !== undefined) return direct;
    // Composite → resolve to composed component's worker
    const composedId = this.compositeIndex.get(entityId)?.get(componentId);
    if (composedId) {
      return this.components.get(entityId)?.get(composedId);
    }
    return undefined;
  }

  getComponentIds(entityId: EntityId): ComponentId[] {
    const map = this.components.get(entityId);
    return map ? [...map.keys()] : [];
  }
}
