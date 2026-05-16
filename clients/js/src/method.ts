import { type z, toJSONSchema } from "zod";

// --- Method definition types ---

export type MethodDefinition = {
  readonly input?: z.ZodType;
  readonly output?: z.ZodType;
};

// --- Method interface ---

export interface Method<
  N extends string = string,
  D extends MethodDefinition = MethodDefinition,
> {
  readonly __type: "method";
  readonly name: N;
  readonly definition: D;
}

// --- Factory ---

export function defineMethod<
  const N extends string,
  const D extends MethodDefinition,
>(name: N, definition: D): Method<N, D> {
  return {
    __type: "method" as const,
    name,
    definition,
  };
}

// --- Serializable schema ---

export interface MethodSchema {
  readonly input?: Record<string, unknown>;
  readonly output?: Record<string, unknown>;
}

/** Convert a Method's definition to a JSON-serializable schema. */
export function toMethodSchema(method: Method): MethodSchema {
  return {
    input: method.definition.input
      ? (toJSONSchema(method.definition.input) as Record<string, unknown>)
      : undefined,
    output: method.definition.output
      ? (toJSONSchema(method.definition.output) as Record<string, unknown>)
      : undefined,
  };
}

// --- Type inference ---

export type InferMethod<M extends Method> =
  M["definition"]["input"] extends z.ZodType
    ? M["definition"]["output"] extends z.ZodType
      ? (
          input: z.infer<M["definition"]["input"]>,
        ) => Promise<z.infer<M["definition"]["output"]>>
      : (input: z.infer<M["definition"]["input"]>) => Promise<void>
    : M["definition"]["output"] extends z.ZodType
      ? () => Promise<z.infer<M["definition"]["output"]>>
      : () => Promise<void>;

// --- Runtime discrimination ---

export function isMethod(value: unknown): value is Method {
  return (
    typeof value === "object" &&
    value !== null &&
    "__type" in value &&
    (value as Method).__type === "method"
  );
}
