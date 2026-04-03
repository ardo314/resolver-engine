import type { z } from "zod";
import type { NatsConnection, Subscription } from "nats";
import { StringCodec } from "nats";
import type { Component, EntityId } from "@engine/core";
import { WorkerSubjects, getAllComposites } from "@engine/core";

// --- Metadata keys ---

const COMPONENT_KEY = "__worker_component__";
const FIELDS_KEY = "__worker_fields__";

const sc = StringCodec();

// --- Types ---

export interface SerializedFieldInfo {
  readonly name: string;
  readonly schema: z.ZodType;
}

export type ComponentWorkerClass = new () => ComponentWorker;

// --- Base class ---

export abstract class ComponentWorker {
  private subscriptions: Subscription[] = [];

  /**
   * Start the worker for a given entity. Subscribes to property get/set
   * subjects for the component and all its composites.
   */
  start(nc: NatsConnection, entityId: EntityId): void {
    const workerClass = this.constructor as ComponentWorkerClass;
    const component = getWorkerComponent(workerClass);
    const fields = getWorkerFields(workerClass);
    const composites = getAllComposites(component);

    const accessors = buildAccessors(this, fields);
    const targetIds = [
      component.id as string,
      ...composites.map((c) => c.id as string),
    ];

    for (const targetId of targetIds) {
      this.subscribeGetProperty(nc, targetId, entityId as string, accessors);
      this.subscribeSetProperty(nc, targetId, entityId as string, accessors);
    }
  }

  /** Stop the worker, unsubscribing from all topics. */
  stop(): void {
    for (const sub of this.subscriptions) sub.unsubscribe();
    this.subscriptions = [];
  }

  private subscribeGetProperty(
    nc: NatsConnection,
    componentId: string,
    entityId: string,
    accessors: Record<
      string,
      { get(): Promise<unknown>; set(value: unknown): Promise<void> }
    >,
  ): void {
    const sub = nc.subscribe(WorkerSubjects.getProperty(componentId, entityId));
    (async () => {
      for await (const msg of sub) {
        try {
          const { property } = JSON.parse(sc.decode(msg.data)) as {
            property: string;
          };
          if (!accessors[property]) {
            msg.respond(
              sc.encode(
                JSON.stringify({ error: `Property ${property} not found` }),
              ),
            );
            continue;
          }
          const value = await accessors[property].get();
          msg.respond(sc.encode(JSON.stringify({ value })));
        } catch (e) {
          const message = e instanceof Error ? e.message : String(e);
          msg.respond(sc.encode(JSON.stringify({ error: message })));
        }
      }
    })();
    this.subscriptions.push(sub);
  }

  private subscribeSetProperty(
    nc: NatsConnection,
    componentId: string,
    entityId: string,
    accessors: Record<
      string,
      { get(): Promise<unknown>; set(value: unknown): Promise<void> }
    >,
  ): void {
    const sub = nc.subscribe(WorkerSubjects.setProperty(componentId, entityId));
    (async () => {
      for await (const msg of sub) {
        try {
          const { property, value } = JSON.parse(sc.decode(msg.data)) as {
            property: string;
            value: unknown;
          };
          if (!accessors[property]) {
            msg.respond(
              sc.encode(
                JSON.stringify({ error: `Property ${property} not found` }),
              ),
            );
            continue;
          }
          await accessors[property].set(value);
          msg.respond(sc.encode(JSON.stringify({ ok: true })));
        } catch (e) {
          const message = e instanceof Error ? e.message : String(e);
          msg.respond(sc.encode(JSON.stringify({ error: message })));
        }
      }
    })();
    this.subscriptions.push(sub);
  }
}

// --- Decorators ---

export function Implements(component: Component) {
  return function <T extends abstract new (...args: any[]) => ComponentWorker>(
    target: T,
    context: ClassDecoratorContext<T>,
  ) {
    context.metadata[COMPONENT_KEY] = component;
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

export function getWorkerFields(
  workerClass: ComponentWorkerClass,
): readonly SerializedFieldInfo[] {
  const metadata = workerClass[Symbol.metadata];
  return (metadata?.[FIELDS_KEY] as SerializedFieldInfo[] | undefined) ?? [];
}

export function getWorkerComponent(
  workerClass: ComponentWorkerClass,
): Component {
  const metadata = workerClass[Symbol.metadata];
  const component = metadata?.[COMPONENT_KEY] as Component | undefined;
  if (!component) {
    throw new Error(`Worker ${workerClass.name} has no @Implements decorator`);
  }
  return component;
}

// --- Accessor helpers ---

function buildAccessors(
  instance: ComponentWorker,
  fields: readonly SerializedFieldInfo[],
): Record<
  string,
  { get(): Promise<unknown>; set(value: unknown): Promise<void> }
> {
  const accessors: Record<
    string,
    { get(): Promise<unknown>; set(value: unknown): Promise<void> }
  > = {};
  for (const field of fields) {
    const key = field.name;
    accessors[key] = {
      async get() {
        return (instance as unknown as Record<string, unknown>)[key];
      },
      async set(value: unknown) {
        (instance as unknown as Record<string, unknown>)[key] =
          field.schema.parse(value);
      },
    };
  }
  return accessors;
}
