import type { EntityId, ComponentId, SchemaId } from "@engine/core";

export class EntityRepository {
  private readonly entities = new Set<EntityId>();
  private nextId = 0;

  /** entityId → componentId → worker proxy instance */
  private readonly components = new Map<EntityId, Map<ComponentId, unknown>>();

  /** entityId → schemaId → componentId (for fast schema lookup) */
  private readonly schemaIndex = new Map<
    EntityId,
    Map<SchemaId, ComponentId>
  >();

  create(): EntityId {
    const id = `${Date.now()}-${this.nextId++}` as EntityId;
    this.entities.add(id);
    this.components.set(id, new Map());
    this.schemaIndex.set(id, new Map());
    return id;
  }

  delete(id: EntityId): boolean {
    this.components.delete(id);
    this.schemaIndex.delete(id);
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
    schemaIds: SchemaId[],
    workerInstance: unknown,
  ): void {
    const entityComponents = this.components.get(entityId);
    const entitySchemas = this.schemaIndex.get(entityId);
    if (!entityComponents || !entitySchemas) {
      throw new Error(`Entity ${entityId as string} does not exist`);
    }
    if (entityComponents.has(componentId)) {
      throw new Error(
        `Component ${componentId as string} already exists on entity ${entityId as string}`,
      );
    }
    for (const schemaId of schemaIds) {
      if (entitySchemas.has(schemaId)) {
        const existing = entitySchemas.get(schemaId)!;
        throw new Error(
          `Schema ${schemaId as string} already provided by component ${existing as string} on entity ${entityId as string}`,
        );
      }
    }
    entityComponents.set(componentId, workerInstance);
    for (const schemaId of schemaIds) {
      entitySchemas.set(schemaId, componentId);
    }
  }

  removeComponent(
    entityId: EntityId,
    componentId: ComponentId,
    schemaIds: SchemaId[],
  ): boolean {
    const entityComponents = this.components.get(entityId);
    const entitySchemas = this.schemaIndex.get(entityId);
    if (!entityComponents || !entitySchemas) return false;
    if (!entityComponents.delete(componentId)) return false;
    for (const schemaId of schemaIds) {
      entitySchemas.delete(schemaId);
    }
    return true;
  }

  hasComponent(entityId: EntityId, componentId: ComponentId): boolean {
    return this.components.get(entityId)?.has(componentId) ?? false;
  }

  hasSchema(entityId: EntityId, schemaId: SchemaId): boolean {
    return this.schemaIndex.get(entityId)?.has(schemaId) ?? false;
  }

  getWorkerInstance(
    entityId: EntityId,
    componentId: ComponentId,
  ): unknown | undefined {
    return this.components.get(entityId)?.get(componentId);
  }

  getComponentIds(entityId: EntityId): ComponentId[] {
    const map = this.components.get(entityId);
    return map ? [...map.keys()] : [];
  }

  getSchemaIdsForComponent(
    entityId: EntityId,
    componentId: ComponentId,
  ): SchemaId[] {
    const entitySchemas = this.schemaIndex.get(entityId);
    if (!entitySchemas) return [];
    const result: SchemaId[] = [];
    for (const [schemaId, cId] of entitySchemas) {
      if (cId === componentId) result.push(schemaId);
    }
    return result;
  }

  getWorkerInstanceBySchema(
    entityId: EntityId,
    schemaId: SchemaId,
  ): unknown | undefined {
    const componentId = this.schemaIndex.get(entityId)?.get(schemaId);
    if (!componentId) return undefined;
    return this.components.get(entityId)?.get(componentId);
  }
}
