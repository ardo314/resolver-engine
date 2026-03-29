import {
  Component,
  ComponentId,
  defineComponent,
  InferComponentMethod,
  ComponentMethodSchema,
  ComponentPropertySchema,
} from "@engine/core";
import { z } from "zod";

export {};

type Capitalize<S extends string> = S extends `${infer F}${infer R}`
  ? `${Uppercase<F>}${R}`
  : S;

type PropertyGetters<P extends Record<string, ComponentPropertySchema>> = {
  [K in keyof P & string as `get${Capitalize<K>}`]: () => Promise<
    z.infer<P[K]>
  >;
};

type PropertySetters<P extends Record<string, ComponentPropertySchema>> = {
  [K in keyof P & string as `set${Capitalize<K>}`]: (
    value: z.infer<P[K]>,
  ) => Promise<void>;
};

type ModuleMethods<M extends Record<string, ComponentMethodSchema>> = {
  [K in keyof M]: InferComponentMethod<M[K]>;
};

export type ComponentModule<T extends Component> =
  (T["schema"]["properties"] extends Record<string, ComponentPropertySchema>
    ? PropertyGetters<T["schema"]["properties"]> &
        PropertySetters<T["schema"]["properties"]>
    : unknown) &
    (T["schema"]["methods"] extends Record<string, ComponentMethodSchema>
      ? ModuleMethods<T["schema"]["methods"]>
      : unknown);

const MyComponent = defineComponent("blubb" as ComponentId, {
  properties: {
    health: z.number(),
    name: z.string(),
  },
  methods: {
    damage: { input: z.number() },
    getName: { output: z.string() },
  },
});

type MyComponentType = typeof MyComponent;

class MyModule implements ComponentModule<typeof MyComponent> {
  async getHealth() {
    return 100;
  }
  async setHealth(_value: number) {}
  async getName() {
    return "test";
  }
  async setName(_value: string) {}
  async damage(_input: number) {}
}
