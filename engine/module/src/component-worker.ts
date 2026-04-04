import type { z } from "zod";
import type { NatsConnection, Subscription } from "nats";
import { StringCodec } from "nats";
import type {
  Component,
  ComponentMethodDefinition,
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

interface PropertyAccessor {
  get(): unknown | Promise<unknown>;
  set(value: unknown): void | Promise<void>;
}

export type ComponentWorkerClass = new () => ComponentWorker;

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

export function Implements(component: Component) {
  return function <T extends abstract new (...args: any[]) => ComponentWorker>(
    target: T,
    context: ClassDecoratorContext<T>,
  ) {
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
