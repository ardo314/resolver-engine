import { randomUUID } from "node:crypto";

/**
 * Uniquely identifies an Entity within the world.
 */
export class EntityId {
  readonly value: string;

  constructor(value?: string) {
    this.value = value ?? randomUUID();
  }

  static new(): EntityId {
    return new EntityId();
  }

  toString(): string {
    return this.value;
  }

  equals(other: EntityId): boolean {
    return this.value === other.value;
  }
}
