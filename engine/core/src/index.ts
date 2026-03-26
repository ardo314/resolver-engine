/**
 * A serializable unique identifier for an entity.
 */
export type EntityId = string & { readonly __brand: unique symbol };
