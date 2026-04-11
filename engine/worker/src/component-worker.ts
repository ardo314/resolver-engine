import type { z } from "zod";
import type { NatsConnection, Subscription } from "nats";
import { StringCodec } from "nats";
import type {
  Component,
  ComponentDefinition,
  ComponentMethodDefinition,
  ComponentPropertyDefinition,
  EntityId,
} from "@engine/core";
import {
  WorkerSubjects,
  getAllComposites,
  getAllProperties,
  getAllMethods,
} from "@engine/core";

// --- Metadata keys ---

const COMPONENT_KEY = "__worker_component__";

const sc = StringCodec();

// --- Types ---

import type { ComponentProperty } from "./component-property.js";

interface PropertyAccessor {
  get(): unknown | Promise<unknown>;
  set(value: unknown): void | Promise<void>;
}

export type ComponentWorkerClass = new () => ComponentWorker;

// --- Worker implementation type ---

type InferWorkerProperties<
  P extends Record<string, ComponentPropertyDefinition>,
> = {
  [K in keyof P]: ComponentProperty<z.infer<P[K]>>;
};

type InferWorkerMethod<M extends ComponentMethodDefinition> =
  M["input"] extends z.ZodType
    ? M["output"] extends z.ZodType
      ? (
          input: z.infer<M["input"]>,
        ) => z.infer<M["output"]> | Promise<z.infer<M["output"]>>
      : (input: z.infer<M["input"]>) => void | Promise<void>
    : M["output"] extends z.ZodType
      ? () => z.infer<M["output"]> | Promise<z.infer<M["output"]>>
      : () => void | Promise<void>;

type InferWorkerMethods<M extends Record<string, ComponentMethodDefinition>> = {
  [K in keyof M]: InferWorkerMethod<M[K]>;
};

type OwnWorkerImpl<D extends ComponentDefinition> =
  (D["properties"] extends Record<string, ComponentPropertyDefinition>
    ? InferWorkerProperties<D["properties"]>
    : unknown) &
    (D["methods"] extends Record<string, ComponentMethodDefinition>
      ? InferWorkerMethods<D["methods"]>
      : unknown);

type UnionToIntersection<U> = (
  U extends unknown ? (k: U) => void : never
) extends (k: infer I) => void
  ? I
  : never;

type CompositeWorkerImpls<CS extends readonly Component[]> =
  CS extends readonly (infer C extends Component)[]
    ? UnionToIntersection<WorkerImplementation<C>>
    : unknown;

export type WorkerImplementation<C extends Component> = OwnWorkerImpl<
  C["definition"]
> &
  (C["definition"]["composites"] extends readonly Component[]
    ? CompositeWorkerImpls<C["definition"]["composites"]>
    : unknown);

// --- Base class ---

export abstract class ComponentWorker {
  private subscriptions: Subscription[] = [];

  /**
   * Start the worker for a given entity. Subscribes to per-property get/set
   * and per-method subjects for the component and all its composites.
   */
  start(nc: NatsConnection, entityId: EntityId): void {
    const workerClass = this.constructor as ComponentWorkerClass;
    const component = getWorkerComponent(workerClass);
    const composites = getAllComposites(component);

    const allProperties = getAllProperties(component);
    const allMethods = getAllMethods(component);

    const targetIds = [
      component.id as string,
      ...composites.map((c) => c.id as string),
    ];

    for (const targetId of targetIds) {
      for (const [name, schema] of Object.entries(allProperties)) {
        this.subscribeGetProperty(nc, targetId, entityId as string, name);
        this.subscribeSetProperty(
          nc,
          targetId,
          entityId as string,
          name,
          schema,
        );
      }
      for (const [name, def] of Object.entries(allMethods)) {
        this.subscribeMethod(nc, targetId, entityId as string, name, def);
      }
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
    property: string,
  ): void {
    const instance = this as unknown as Record<string, PropertyAccessor>;
    const accessor = instance[property];
    if (!accessor || typeof accessor.get !== "function") {
      throw new Error(
        `Worker ${this.constructor.name} does not implement get for property "${property}"`,
      );
    }
    const sub = nc.subscribe(
      WorkerSubjects.getProperty(componentId, entityId, property),
    );
    (async () => {
      for await (const msg of sub) {
        try {
          const value = await accessor.get();
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
    property: string,
    schema: z.ZodType,
  ): void {
    const instance = this as unknown as Record<string, PropertyAccessor>;
    const accessor = instance[property];
    if (!accessor || typeof accessor.set !== "function") {
      throw new Error(
        `Worker ${this.constructor.name} does not implement set for property "${property}"`,
      );
    }
    const sub = nc.subscribe(
      WorkerSubjects.setProperty(componentId, entityId, property),
    );
    (async () => {
      for await (const msg of sub) {
        try {
          const { value } = JSON.parse(sc.decode(msg.data)) as {
            value: unknown;
          };
          await accessor.set(schema.parse(value));
          msg.respond(sc.encode(JSON.stringify({ ok: true })));
        } catch (e) {
          const message = e instanceof Error ? e.message : String(e);
          msg.respond(sc.encode(JSON.stringify({ error: message })));
        }
      }
    })();
    this.subscriptions.push(sub);
  }

  private subscribeMethod(
    nc: NatsConnection,
    componentId: string,
    entityId: string,
    method: string,
    def: ComponentMethodDefinition,
  ): void {
    const instance = this as unknown as Record<string, unknown>;
    const fn = instance[method];
    if (typeof fn !== "function") {
      throw new Error(
        `Worker ${this.constructor.name} does not implement method "${method}"`,
      );
    }

    const sub = nc.subscribe(
      WorkerSubjects.callMethod(componentId, entityId, method),
    );
    (async () => {
      for await (const msg of sub) {
        try {
          let input: unknown;
          if (def.input) {
            const payload = JSON.parse(sc.decode(msg.data)) as {
              input: unknown;
            };
            input = def.input.parse(payload.input);
          }
          const raw = await (fn as Function).call(instance, input);
          const result = def.output ? def.output.parse(raw) : undefined;
          msg.respond(sc.encode(JSON.stringify({ result })));
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

export function Implements<C extends Component>(component: C) {
  return function <
    T extends abstract new (
      ...args: any[]
    ) => ComponentWorker & WorkerImplementation<C>,
  >(target: T, context: ClassDecoratorContext<T>) {
    context.metadata[COMPONENT_KEY] = component;
  };
}

// --- Metadata access ---

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
