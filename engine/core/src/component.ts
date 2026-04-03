import type { z } from "zod";

// --- Branded ID ---

export type ComponentId = string & { readonly __brand: unique symbol };

// --- Property & method definition types ---

export type ComponentPropertyDefinition = z.ZodType;

export type ComponentMethodDefinition = {
  readonly input?: z.ZodType;
  readonly output?: z.ZodType;
};

// --- Component definition ---

export type ComponentDefinition = {
  readonly properties?: Record<string, ComponentPropertyDefinition>;
  readonly methods?: Record<string, ComponentMethodDefinition>;
  readonly composites?: readonly Component[];
};

// --- Component interface ---

export interface Component<
  D extends ComponentDefinition = ComponentDefinition,
> {
  readonly __type: "component";
  readonly id: ComponentId;
  readonly definition: D;
}

// --- Factory ---

export function defineComponent<const D extends ComponentDefinition>(
  id: string,
  definition: D,
): Component<D> {
  // Validate no property name conflicts across composites + own properties
  validatePropertyConflicts(id, definition);

  return {
    __type: "component" as const,
    id: id as ComponentId,
    definition,
  };
}

// --- Conflict validation ---

function collectPropertyNames(
  definition: ComponentDefinition,
  visited: Set<string>,
): string[] {
  const names: string[] = [];
  if (definition.properties) {
    names.push(...Object.keys(definition.properties));
  }
  for (const composite of definition.composites ?? []) {
    const compositeId = composite.id as string;
    if (visited.has(compositeId)) continue;
    visited.add(compositeId);
    names.push(...collectPropertyNames(composite.definition, visited));
  }
  return names;
}

function validatePropertyConflicts(
  id: string,
  definition: ComponentDefinition,
): void {
  const visited = new Set<string>();
  const names = collectPropertyNames(definition, visited);
  const seen = new Set<string>();
  for (const name of names) {
    if (seen.has(name)) {
      throw new Error(
        `Component "${id}": duplicate property name "${name}" across composites`,
      );
    }
    seen.add(name);
  }
}

// --- Helpers ---

/** Recursively collect all composite components (depth-first, deduplicated). */
export function getAllComposites(component: Component): Component[] {
  const result: Component[] = [];
  const visited = new Set<string>();
  function walk(comp: Component) {
    for (const composite of comp.definition.composites ?? []) {
      const cid = composite.id as string;
      if (visited.has(cid)) continue;
      visited.add(cid);
      result.push(composite);
      walk(composite);
    }
  }
  walk(component);
  return result;
}

/** Recursively collect all property names and their zod schemas (own + composites). */
export function getAllProperties(
  component: Component,
): Record<string, z.ZodType> {
  const result: Record<string, z.ZodType> = {};
  const visited = new Set<string>();
  function walk(comp: Component) {
    if (comp.definition.properties) {
      Object.assign(result, comp.definition.properties);
    }
    for (const composite of comp.definition.composites ?? []) {
      const cid = composite.id as string;
      if (visited.has(cid)) continue;
      visited.add(cid);
      walk(composite);
    }
  }
  walk(component);
  return result;
}

// --- Type inference ---

export type InferComponentProperties<
  P extends Record<string, ComponentPropertyDefinition>,
> = {
  [K in keyof P]: {
    get(): Promise<z.infer<P[K]>>;
    set(value: z.infer<P[K]>): Promise<void>;
  };
};

export type InferComponentMethod<M extends ComponentMethodDefinition> =
  M["input"] extends z.ZodType
    ? M["output"] extends z.ZodType
      ? (input: z.infer<M["input"]>) => Promise<z.infer<M["output"]>>
      : (input: z.infer<M["input"]>) => Promise<void>
    : M["output"] extends z.ZodType
      ? () => Promise<z.infer<M["output"]>>
      : () => Promise<void>;

export type InferComponentMethods<
  M extends Record<string, ComponentMethodDefinition>,
> = {
  [K in keyof M]: InferComponentMethod<M[K]>;
};

// Own properties + methods for a single component definition
type OwnReference<D extends ComponentDefinition> =
  (D["properties"] extends Record<string, ComponentPropertyDefinition>
    ? InferComponentProperties<D["properties"]>
    : unknown) &
    (D["methods"] extends Record<string, ComponentMethodDefinition>
      ? InferComponentMethods<D["methods"]>
      : unknown);

// Composite references: union of composites → intersection
type UnionToIntersection<U> = (
  U extends unknown ? (k: U) => void : never
) extends (k: infer I) => void
  ? I
  : never;

type CompositeReferences<CS extends readonly Component[]> =
  CS extends readonly (infer C extends Component)[]
    ? UnionToIntersection<ComponentReference<C>>
    : unknown;

/** Full typed reference: own properties/methods + all composite references. */
export type ComponentReference<C extends Component> = OwnReference<
  C["definition"]
> &
  (C["definition"]["composites"] extends readonly Component[]
    ? CompositeReferences<C["definition"]["composites"]>
    : unknown);

// --- Runtime discrimination ---

export function isComponent(value: unknown): value is Component {
  return (
    typeof value === "object" &&
    value !== null &&
    "__type" in value &&
    (value as Component).__type === "component"
  );
}
