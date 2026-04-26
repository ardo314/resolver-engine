import type { z } from "zod";
import type { NatsConnection, Subscription } from "nats";
import { StringCodec } from "nats";
import type {
  Component,
  Method,
  MethodDefinition,
  EntityId,
} from "@engine/core";
import { WorkerSubjects } from "@engine/core";

// --- Symbol.metadata polyfill (needed for TC39 decorators) ---
(Symbol as any).metadata ??= Symbol("Symbol.metadata");

// --- Metadata keys ---

const COMPONENT_KEY = "__worker_component__";

const sc = StringCodec();

// --- Types ---

export type ComponentWorkerClass = new () => ComponentWorker;

// --- Worker implementation type ---

type InferWorkerMethod<M extends MethodDefinition> =
  M["input"] extends z.ZodType
    ? M["output"] extends z.ZodType
      ? (
          input: z.infer<M["input"]>,
        ) => z.infer<M["output"]> | Promise<z.infer<M["output"]>>
      : (input: z.infer<M["input"]>) => void | Promise<void>
    : M["output"] extends z.ZodType
      ? () => z.infer<M["output"]> | Promise<z.infer<M["output"]>>
      : () => void | Promise<void>;

type WorkerMethodImpl<M> = M extends Method<infer N, infer D>
  ? { [K in N]: InferWorkerMethod<D> }
  : never;

type UnionToIntersection<U> = (
  U extends unknown ? (k: U) => void : never
) extends (k: infer I) => void
  ? I
  : never;

export type WorkerImplementation<C extends Component> =
  C["methods"] extends readonly (infer M)[]
    ? UnionToIntersection<WorkerMethodImpl<M>>
    : never;

// --- Base class ---

export abstract class ComponentWorker {
  private subscriptions: Subscription[] = [];
  protected nc!: NatsConnection;
  protected entityId!: EntityId;

  /** Called after the worker is fully wired (all subscriptions active). */
  protected onAdded?(): void | Promise<void>;
  /** Called before the worker is torn down (subscriptions still active). */
  protected onRemoved?(): void | Promise<void>;

  /**
   * Start the worker for a given entity. Subscribes to per-method subjects
   * for the component, then invokes the onAdded lifecycle hook.
   */
  async start(nc: NatsConnection, entityId: EntityId): Promise<void> {
    this.nc = nc;
    this.entityId = entityId;

    const workerClass = this.constructor as ComponentWorkerClass;
    const component = getWorkerComponent(workerClass);
    const componentId = component.id as string;

    for (const method of component.methods) {
      this.subscribeMethod(nc, componentId, entityId as string, method);
    }

    await this.onAdded?.();
  }

  /** Stop the worker: invokes onRemoved, then unsubscribes from all topics. */
  async stop(): Promise<void> {
    await this.onRemoved?.();
    for (const sub of this.subscriptions) sub.unsubscribe();
    this.subscriptions = [];
  }

  private subscribeMethod(
    nc: NatsConnection,
    componentId: string,
    entityId: string,
    method: Method,
  ): void {
    const instance = this as unknown as Record<string, unknown>;
    const fn = instance[method.name];
    if (typeof fn !== "function") {
      throw new Error(
        `Worker ${this.constructor.name} does not implement method "${method.name}"`,
      );
    }

    const sub = nc.subscribe(
      WorkerSubjects.callMethod(componentId, entityId, method.name),
    );
    (async () => {
      for await (const msg of sub) {
        try {
          let input: unknown;
          if (method.definition.input) {
            const payload = JSON.parse(sc.decode(msg.data)) as {
              input: unknown;
            };
            input = method.definition.input.parse(payload.input);
          }
          const raw = await (fn as Function).call(instance, input);
          const result = method.definition.output
            ? method.definition.output.parse(raw)
            : undefined;
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
