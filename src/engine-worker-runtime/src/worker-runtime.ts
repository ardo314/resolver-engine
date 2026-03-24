import { EntityId } from "engine-core";
import { ComponentWorker, WorkerRegistration } from "engine-worker";
import {
  connect,
  NatsConnection,
  StringCodec,
  Subscription,
  headers,
} from "nats";

const sc = StringCodec();

/**
 * Worker runtime that hosts ComponentWorker instances, subscribes to NATS
 * subjects for worker lifecycle and component method dispatch.
 */
export class WorkerRuntime {
  private readonly nats: NatsConnection;
  private readonly registrations: WorkerRegistration[];
  private readonly subscriptions: Subscription[] = [];

  /** Live worker instances keyed by "entityId:componentName" */
  private readonly workers = new Map<string, ComponentWorker>();

  /** Maps "entityId:behaviourName" → worker instance for dispatch routing */
  private readonly behaviourWorkers = new Map<string, ComponentWorker>();

  /** Maps behaviour name → component name for dispatch routing */
  private readonly behaviourToComponent = new Map<string, string>();

  constructor(nats: NatsConnection, registrations: WorkerRegistration[]) {
    this.nats = nats;
    this.registrations = registrations;

    // Build behaviour→component mapping
    for (const reg of registrations) {
      for (const behaviourName of reg.behaviourNames) {
        this.behaviourToComponent.set(behaviourName, reg.componentName);
        console.log(`  Behaviour: ${behaviourName} → ${reg.componentName}`);
      }
    }
  }

  async start(): Promise<void> {
    // Subscribe to worker lifecycle subjects for each registration
    for (const reg of this.registrations) {
      this.subscribeCreate(reg);
      this.subscribeRemove(reg);
    }

    // Subscribe to component method dispatch subjects
    const allBehaviourNames = new Set(this.behaviourToComponent.keys());
    for (const behaviourName of allBehaviourNames) {
      this.subscribeDispatch(behaviourName);
    }

    console.log(
      `Registered ${this.registrations.length} component worker type(s).`,
    );
  }

  private subscribeCreate(reg: WorkerRegistration): void {
    const sub = this.nats.subscribe(`worker.create.${reg.componentName}`);
    this.subscriptions.push(sub);

    (async () => {
      for await (const msg of sub) {
        try {
          const entityIdStr = sc.decode(msg.data);
          const entityId = new EntityId(entityIdStr);
          const key = `${entityId.value}:${reg.componentName}`;

          if (this.workers.has(key)) {
            msg.respond(
              sc.encode("error: worker already exists for this entity"),
            );
            continue;
          }

          const instance = reg.create();
          instance.entityId = entityId;
          await instance.onAdded();

          this.workers.set(key, instance);

          // Register for all behaviour interfaces
          for (const behaviourName of reg.behaviourNames) {
            this.behaviourWorkers.set(
              `${entityId.value}:${behaviourName}`,
              instance,
            );
          }

          console.log(
            `Created worker ${reg.componentName} for entity ${entityId}`,
          );
          msg.respond(sc.encode("ok"));
        } catch (err) {
          console.error("Error creating worker:", err);
          msg.respond(sc.encode(`error: ${err}`));
        }
      }
    })();
  }

  private subscribeRemove(reg: WorkerRegistration): void {
    const sub = this.nats.subscribe(`worker.remove.${reg.componentName}`);
    this.subscriptions.push(sub);

    (async () => {
      for await (const msg of sub) {
        try {
          const entityIdStr = sc.decode(msg.data);
          const entityId = new EntityId(entityIdStr);
          const key = `${entityId.value}:${reg.componentName}`;

          const instance = this.workers.get(key);
          if (!instance) {
            msg.respond(sc.encode("error: no worker found for this entity"));
            continue;
          }

          await instance.onRemoved();
          this.workers.delete(key);

          // Unregister behaviour interfaces
          for (const behaviourName of reg.behaviourNames) {
            this.behaviourWorkers.delete(`${entityId.value}:${behaviourName}`);
          }

          console.log(
            `Removed worker ${reg.componentName} for entity ${entityId}`,
          );
          msg.respond(sc.encode("ok"));
        } catch (err) {
          console.error("Error removing worker:", err);
          msg.respond(sc.encode(`error: ${err}`));
        }
      }
    })();
  }

  private subscribeDispatch(behaviourName: string): void {
    // Subscribe to component.<behaviourName>.* (wildcard for all methods)
    const sub = this.nats.subscribe(`component.${behaviourName}.*`);
    this.subscriptions.push(sub);

    (async () => {
      for await (const msg of sub) {
        try {
          // Extract method name from subject: component.<name>.<method>
          const parts = msg.subject.split(".");
          const methodName = parts[parts.length - 1];

          // EntityId can come from a header or from the payload
          let entityId: EntityId;
          let dispatchPayload: Uint8Array;

          const entityIdHeader = msg.headers?.get("EntityId");
          if (entityIdHeader && entityIdHeader.length > 0) {
            entityId = new EntityId(entityIdHeader[0]);
            dispatchPayload = msg.data ?? new Uint8Array();
          } else {
            // Payload is a UTF-8 EntityId string (no-param methods)
            const payloadStr = sc
              .decode(msg.data)
              .replace(/\0/g, "")
              .replace(/"/g, "");
            entityId = new EntityId(payloadStr);
            dispatchPayload = new Uint8Array();
          }

          const key = `${entityId.value}:${behaviourName}`;
          const instance = this.behaviourWorkers.get(key);

          if (!instance) {
            msg.respond(sc.encode("error: no worker found for this entity"));
            continue;
          }

          const result = await instance.dispatch(
            behaviourName,
            methodName,
            dispatchPayload,
          );

          if (result.length > 0) {
            msg.respond(result);
          } else {
            msg.respond(sc.encode("ok"));
          }
        } catch (err) {
          console.error("Error dispatching component method:", err);
          msg.respond(sc.encode(`error: ${err}`));
        }
      }
    })();
  }

  async stop(): Promise<void> {
    for (const sub of this.subscriptions) {
      sub.unsubscribe();
    }
  }
}

/**
 * Starts the worker runtime with the given registrations.
 * This is the main entry point for module runtimes.
 */
export async function startWorkerRuntime(
  registrations: WorkerRegistration[],
): Promise<void> {
  const nc = await connect();
  console.log("Connected to NATS.");

  const runtime = new WorkerRuntime(nc, registrations);
  await runtime.start();

  console.log("Engine.WorkerRuntime running – press Ctrl+C to stop.");

  const shutdown = async () => {
    console.log("\nShutting down...");
    await runtime.stop();
    await nc.drain();
    process.exit(0);
  };

  process.on("SIGINT", shutdown);
  process.on("SIGTERM", shutdown);

  await nc.closed();
}
