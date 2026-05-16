import type { Method, InferMethod } from "./method.js";

// --- Query interface ---

export interface Query<MS extends readonly Method[] = readonly Method[]> {
  readonly __type: "query";
  readonly methods: MS;
}

// --- Factory ---

export function defineQuery<const MS extends readonly Method[]>(
  methods: MS,
): Query<MS> {
  return {
    __type: "query" as const,
    methods,
  };
}

// --- Type inference ---

type UnionToIntersection<U> = (
  U extends unknown ? (k: U) => void : never
) extends (k: infer I) => void
  ? I
  : never;

type MethodRefEntry<M> =
  M extends Method<infer N, infer _D> ? { [K in N]: InferMethod<M> } : never;

/** Typed reference produced by querying an entity: intersection of all method signatures keyed by name. */
export type QueryReference<Q extends Query> =
  Q["methods"] extends readonly (infer M)[]
    ? UnionToIntersection<MethodRefEntry<M>>
    : never;

// --- Runtime discrimination ---

export function isQuery(value: unknown): value is Query {
  return (
    typeof value === "object" &&
    value !== null &&
    "__type" in value &&
    (value as Query).__type === "query"
  );
}
