import type { EntityId } from "@engine/core";

let nextId = 0;

export function createEntityId(): EntityId {
  return `${Date.now()}-${nextId++}` as EntityId;
}
