import type { z } from "zod";
import type { Component, Schema } from "@engine/core";
import { defineComponent } from "@engine/core";

// --- Metadata keys ---

const SCHEMAS_KEY = "__worker_schemas__";
const FIELDS_KEY = "__worker_fields__";

// --- Types ---

export interface SerializedFieldInfo {
  readonly name: string;
  readonly schema: z.ZodType;
}

export type ComponentWorkerClass = new () => ComponentWorker;

// --- Base class ---

export abstract class ComponentWorker {}

// --- Decorators ---

export function Implements(...schemas: Schema[]) {
  return function <T extends abstract new (...args: any[]) => ComponentWorker>(
    target: T,
    context: ClassDecoratorContext<T>,
  ) {
    context.metadata[SCHEMAS_KEY] = schemas;
  };
}

export function SerializeField(schema: z.ZodType) {
  return function (_target: undefined, context: ClassFieldDecoratorContext) {
    const fields = (context.metadata[FIELDS_KEY] ??=
      []) as SerializedFieldInfo[];
    fields.push({ name: context.name as string, schema });
  };
}

// --- Metadata access ---

export function getWorkerSchemas(
  workerClass: ComponentWorkerClass,
): readonly Schema[] {
  const metadata = workerClass[Symbol.metadata];
  return (metadata?.[SCHEMAS_KEY] as Schema[] | undefined) ?? [];
}

export function getWorkerFields(
  workerClass: ComponentWorkerClass,
): readonly SerializedFieldInfo[] {
  const metadata = workerClass[Symbol.metadata];
  return (metadata?.[FIELDS_KEY] as SerializedFieldInfo[] | undefined) ?? [];
}

export function getWorkerComponent(
  workerClass: ComponentWorkerClass,
): Component {
  const schemas = getWorkerSchemas(workerClass);
  if (schemas.length === 0) {
    throw new Error(`Worker ${workerClass.name} has no @Implements decorator`);
  }
  return defineComponent(...schemas);
}

// --- Instance creation ---

export function createWorkerAccessors(workerClass: ComponentWorkerClass): {
  instance: ComponentWorker;
  accessors: Record<
    string,
    { get(): Promise<unknown>; set(value: unknown): Promise<void> }
  >;
} {
  const instance = new workerClass();
  const fields = getWorkerFields(workerClass);
  const accessors: Record<
    string,
    { get(): Promise<unknown>; set(value: unknown): Promise<void> }
  > = {};

  for (const field of fields) {
    const key = field.name;
    accessors[key] = {
      async get() {
        return (instance as Record<string, unknown>)[key];
      },
      async set(value: unknown) {
        (instance as Record<string, unknown>)[key] = field.schema.parse(value);
      },
    };
  }

  return { instance, accessors };
}
