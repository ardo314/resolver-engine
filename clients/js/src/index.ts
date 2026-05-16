// Core types
export { entityIdSchema } from "./entity-id.js";
export type { EntityId } from "./entity-id.js";
export { defineMethod, isMethod, toMethodSchema } from "./method.js";
export type {
  Method,
  MethodDefinition,
  MethodSchema,
  InferMethod,
} from "./method.js";
export { defineQuery, isQuery } from "./query.js";
export type { Query, QueryReference } from "./query.js";
export {
  defineComponent,
  isComponent,
  toComponentSchema,
} from "./component.js";
export type {
  Component,
  ComponentId,
  ComponentReference,
  ComponentSchema,
} from "./component.js";
export { Subjects, WorkerSubjects } from "./subjects.js";

// Client
export { World, type RegisteredComponent } from "./world.js";
export { Entity } from "./entity.js";
