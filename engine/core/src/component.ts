import type { Schema, SchemaProxy } from "./schema.js";

export type ComponentId = string & { readonly __brand: unique symbol };

export interface Component<S extends Schema[] = Schema[]> {
  readonly __type: "component";
  readonly id: ComponentId;
  readonly schemas: S;
}

export function defineComponent<const S extends Schema[]>(
  ...schemas: S
): Component<S> {
  const id = schemas
    .map((s) => s.id as string)
    .sort()
    .join("|") as unknown as ComponentId;
  return {
    __type: "component" as const,
    id,
    schemas,
  };
}

type UnionToIntersection<U> = (
  U extends unknown ? (k: U) => void : never
) extends (k: infer I) => void
  ? I
  : never;

export type ComponentReference<C extends Component> = UnionToIntersection<
  SchemaProxy<C["schemas"][number]>
>;

export function isComponent(value: unknown): value is Component {
  return (
    typeof value === "object" &&
    value !== null &&
    "__type" in value &&
    (value as Component).__type === "component"
  );
}
