import { z } from "zod";

export type SchemaId = string & { readonly __brand: unique symbol };

export type SchemaPropertyDefinition = z.ZodType;

export type SchemaMethodDefinition = {
  readonly input?: z.ZodType;
  readonly output?: z.ZodType;
};

export type SchemaDefinition = {
  readonly properties?: Record<string, SchemaPropertyDefinition>;
  readonly methods?: Record<string, SchemaMethodDefinition>;
};

export interface Schema<D extends SchemaDefinition = SchemaDefinition> {
  readonly __type: "schema";
  readonly id: SchemaId;
  readonly definition: D;
}

export function defineSchema<const D extends SchemaDefinition>(
  id: string,
  definition: D,
): Schema<D> {
  return {
    __type: "schema" as const,
    id: id as SchemaId,
    definition,
  };
}

export type InferSchemaProperties<
  P extends Record<string, SchemaPropertyDefinition>,
> = {
  [K in keyof P]: {
    get(): Promise<z.infer<P[K]>>;
    set(value: z.infer<P[K]>): Promise<void>;
  };
};

export type InferSchemaMethod<M extends SchemaMethodDefinition> =
  M["input"] extends z.ZodType
    ? M["output"] extends z.ZodType
      ? (input: z.infer<M["input"]>) => Promise<z.infer<M["output"]>>
      : (input: z.infer<M["input"]>) => Promise<void>
    : M["output"] extends z.ZodType
      ? () => Promise<z.infer<M["output"]>>
      : () => Promise<void>;

export type InferSchemaMethods<
  M extends Record<string, SchemaMethodDefinition>,
> = {
  [K in keyof M]: InferSchemaMethod<M[K]>;
};

export type SchemaReference<S extends Schema> =
  (S["definition"]["properties"] extends Record<
    string,
    SchemaPropertyDefinition
  >
    ? InferSchemaProperties<S["definition"]["properties"]>
    : unknown) &
    (S["definition"]["methods"] extends Record<string, SchemaMethodDefinition>
      ? InferSchemaMethods<S["definition"]["methods"]>
      : unknown);

export function isSchema(value: unknown): value is Schema {
  return (
    typeof value === "object" &&
    value !== null &&
    "__type" in value &&
    (value as Schema).__type === "schema"
  );
}
