import { z } from "zod";

type Id = string & { readonly __brand: unique symbol };

type MethodSchema = {
  readonly input?: z.ZodType;
  readonly output?: z.ZodType;
};

export type ComponentSchema = {
  readonly properties?: Record<string, z.ZodType>;
  readonly methods?: Record<string, MethodSchema>;
};

interface Component {
  readonly id: Id;
  readonly contract: ComponentSchema;
}

export function defineComponent<const C extends ComponentSchema>(
  id: Id,
  contract: C,
): { readonly id: Id; readonly contract: C } {
  return {
    id: id,
    contract: contract,
  };
}

type InferProperties<P extends Record<string, z.ZodType>> = {
  [K in keyof P]: {
    get(): Promise<z.infer<P[K]>>;
    set(value: z.infer<P[K]>): Promise<void>;
  };
};

type InferMethod<M extends MethodSchema> = M["input"] extends z.ZodType
  ? M["output"] extends z.ZodType
    ? (input: z.infer<M["input"]>) => Promise<z.infer<M["output"]>>
    : (input: z.infer<M["input"]>) => Promise<void>
  : M["output"] extends z.ZodType
    ? () => Promise<z.infer<M["output"]>>
    : () => Promise<void>;

type InferMethods<M extends Record<string, MethodSchema>> = {
  [K in keyof M]: InferMethod<M[K]>;
};

export type ComponentProxy<T extends Component> =
  (T["contract"]["properties"] extends Record<string, z.ZodType>
    ? InferProperties<T["contract"]["properties"]>
    : unknown) &
    (T["contract"]["methods"] extends Record<string, MethodSchema>
      ? InferMethods<T["contract"]["methods"]>
      : unknown);

const MyComponent = defineComponent("position" as Id, {
  properties: {
    x: z.number(),
    y: z.number(),
  },
  methods: {
    moveBy: {
      input: z.object({
        dx: z.number(),
        dy: z.number(),
      }),
    },
    move: {},
    moveFoo: {
      input: z.object({
        x: z.number(),
        y: z.number(),
      }),
      output: z.object({
        success: z.boolean(),
      }),
    },
    distance: {
      output: z.number(),
    },
  },
});

type MyComponentProxy = ComponentProxy<typeof MyComponent>;
