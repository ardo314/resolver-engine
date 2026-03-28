import {
  Component,
  ComponentId,
  defineComponent,
  InferComponentMethods,
  InferComponentProperties,
  ComponentMethodSchema,
  ComponentPropertySchema,
} from "@engine/core";
import { z } from "zod";

export {};

export type ComponentModule<T extends Component> =
  (T["schema"]["properties"] extends Record<string, ComponentPropertySchema>
    ? InferComponentProperties<T["schema"]["properties"]>
    : unknown) &
    (T["schema"]["methods"] extends Record<string, ComponentMethodSchema>
      ? InferComponentMethods<T["schema"]["methods"]>
      : unknown);

const MyComponent = defineComponent("blubb" as ComponentId, {});

class MyModule implements ComponentModule<typeof MyComponent> {}
