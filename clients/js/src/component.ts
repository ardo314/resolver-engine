import type { Method, MethodSchema, InferMethod } from "./method.js";
import { toMethodSchema } from "./method.js";

// --- Branded ID ---

export type ComponentId = string & { readonly __brand: unique symbol };

// --- Component interface ---

export interface Component<MS extends readonly Method[] = readonly Method[]> {
  readonly __type: "component";
  readonly id: ComponentId;
  readonly methods: MS;
}

// --- Factory ---

export function defineComponent<const MS extends readonly Method[]>(
  id: string,
  methods: MS,
): Component<MS> {
  validateMethodConflicts(id, methods);
  return {
    __type: "component" as const,
    id: id as ComponentId,
    methods,
  };
}

// --- Conflict validation ---

function validateMethodConflicts(id: string, methods: readonly Method[]): void {
  const seen = new Set<string>();
  for (const method of methods) {
    if (seen.has(method.name)) {
      throw new Error(
        `Component "${id}": duplicate method name "${method.name}"`,
      );
    }
    seen.add(method.name);
  }
}

// --- Serializable schema ---

export interface ComponentSchema {
  readonly methods: Record<string, MethodSchema>;
}

/** Convert a Component to a JSON-serializable schema. */
export function toComponentSchema(component: Component): ComponentSchema {
  const methods: Record<string, MethodSchema> = {};
  for (const method of component.methods) {
    methods[method.name] = toMethodSchema(method);
  }
  return { methods };
}

// --- Type inference ---

type UnionToIntersection<U> = (
  U extends unknown ? (k: U) => void : never
) extends (k: infer I) => void
  ? I
  : never;

type MethodRefEntry<M> =
  M extends Method<infer N, infer _D> ? { [K in N]: InferMethod<M> } : never;

/** Typed reference for a component: intersection of all method signatures keyed by name. */
export type ComponentReference<C extends Component> =
  C["methods"] extends readonly (infer M)[]
    ? UnionToIntersection<MethodRefEntry<M>>
    : never;

// --- Runtime discrimination ---

export function isComponent(value: unknown): value is Component {
  return (
    typeof value === "object" &&
    value !== null &&
    "__type" in value &&
    (value as Component).__type === "component"
  );
}
