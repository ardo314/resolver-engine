import { EntityId } from "engine-core";

/**
 * Central in-memory store for entity existence and per-entity component sets.
 * All operations are synchronous and thread-safe in the single-threaded Node.js model.
 */
export class EntityRepository {
  private readonly entities = new Map<string, Set<string>>();

  /** Creates a new entity and returns its id. */
  create(): EntityId {
    const id = EntityId.new();
    this.entities.set(id.value, new Set());
    return id;
  }

  /**
   * Removes an entity and all of its components.
   * Returns the list of component names that were attached, or null if the entity did not exist.
   */
  destroy(id: EntityId): string[] | null {
    const set = this.entities.get(id.value);
    if (!set) return null;
    const components = Array.from(set);
    this.entities.delete(id.value);
    return components;
  }

  /** Returns whether the entity exists. */
  exists(id: EntityId): boolean {
    return this.entities.has(id.value);
  }

  /** Returns all known entity ids. */
  listAll(): EntityId[] {
    return Array.from(this.entities.keys()).map((v) => new EntityId(v));
  }

  /** Adds a component to an entity. Returns false if already present. */
  addComponent(id: EntityId, componentName: string): boolean {
    const set = this.entities.get(id.value);
    if (!set) return false;
    if (set.has(componentName)) return false;
    set.add(componentName);
    return true;
  }

  /** Removes a component from an entity. Returns false if not found. */
  removeComponent(id: EntityId, componentName: string): boolean {
    const set = this.entities.get(id.value);
    if (!set) return false;
    return set.delete(componentName);
  }

  /** Checks whether an entity has a given component. */
  hasComponent(id: EntityId, componentName: string): boolean {
    const set = this.entities.get(id.value);
    return set !== undefined && set.has(componentName);
  }

  /** Returns the component names for an entity, or an empty array if none. */
  listComponents(id: EntityId): string[] {
    const set = this.entities.get(id.value);
    return set ? Array.from(set) : [];
  }
}
