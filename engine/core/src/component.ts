import { z } from "zod";

export type ComponentId = string & { readonly __brand: unique symbol };

export type ComponentPropertySchema = z.ZodType;

export type ComponentMethodSchema = {
  readonly input?: z.ZodType;
  readonly output?: z.ZodType;
};

type ComponentSchema = {
  readonly properties?: Record<string, ComponentPropertySchema>;
  readonly methods?: Record<string, ComponentMethodSchema>;
};

export interface Component {
  readonly id: ComponentId;
  readonly schema: ComponentSchema;
}

export function defineComponent<const C extends ComponentSchema>(
  id: ComponentId,
  schema: C,
): { readonly id: ComponentId; readonly schema: C } {
  return {
    id: id,
    schema: schema,
  };
}

export type InferComponentProperties<
  P extends Record<string, ComponentPropertySchema>,
> = {
  [K in keyof P]: {
    get(): Promise<z.infer<P[K]>>;
    set(value: z.infer<P[K]>): Promise<void>;
  };
};

export type InferComponentMethod<M extends ComponentMethodSchema> =
  M["input"] extends z.ZodType
    ? M["output"] extends z.ZodType
      ? (input: z.infer<M["input"]>) => Promise<z.infer<M["output"]>>
      : (input: z.infer<M["input"]>) => Promise<void>
    : M["output"] extends z.ZodType
      ? () => Promise<z.infer<M["output"]>>
      : () => Promise<void>;

export type InferComponentMethods<
  M extends Record<string, ComponentMethodSchema>,
> = {
  [K in keyof M]: InferComponentMethod<M[K]>;
};

export type ComponentProxy<T extends Component> =
  (T["schema"]["properties"] extends Record<string, ComponentPropertySchema>
    ? InferComponentProperties<T["schema"]["properties"]>
    : unknown) &
    (T["schema"]["methods"] extends Record<string, ComponentMethodSchema>
      ? InferComponentMethods<T["schema"]["methods"]>
      : unknown);
