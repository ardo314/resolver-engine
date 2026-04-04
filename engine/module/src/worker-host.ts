import type { NatsConnection } from "nats";
import { StringCodec } from "nats";
import type { ComponentId, EntityId } from "@engine/core";
import { Subjects, getAllComposites } from "@engine/core";
import type { ComponentWorkerClass } from "./component-worker.js";
import { ComponentWorker, getWorkerComponent } from "./component-worker.js";

const sc = StringCodec();

export class WorkerHost {
  private readonly workerClasses = new Map<string, ComponentWorkerClass>();
  /** Live worker instances, keyed by "entityId:componentId" */
  private readonly activeWorkers = new Map<string, ComponentWorker>();

  constructor(private readonly nc: NatsConnection) {}

  registerWorker(workerClass: ComponentWorkerClass): void {
    const component = getWorkerComponent(workerClass);
    this.workerClasses.set(component.id as string, workerClass);
  }

  async listen(): Promise<void> {
    await this.registerComponents();
    this.handleStartWorker();
    this.handleStopWorker();
  }

  private async registerComponents(): Promise<void> {
    for (const [componentId, workerClass] of this.workerClasses) {
      const component = getWorkerComponent(workerClass);
      const composites = getAllComposites(component);
      const compositeIds = composites.map((c) => c.id as string);

      const reply = await this.nc.request(
        Subjects.registerComponent,
        sc.encode(JSON.stringify({ componentId, compositeIds })),
      );
      const result = JSON.parse(sc.decode(reply.data)) as {
        ok?: boolean;
        error?: string;
      };
      if (result.error) {
        throw new Error(
          `Failed to register component ${componentId}: ${result.error}`,
        );
      }
    }
  }

  private handleStartWorker(): void {
    const sub = this.nc.subscribe(Subjects.startWorker);
    (async () => {
      for await (const msg of sub) {
        try {
          const { entityId, componentId } = JSON.parse(sc.decode(msg.data)) as {
            entityId: EntityId;
            componentId: ComponentId;
          };
          const workerClass = this.workerClasses.get(componentId as string);
          if (!workerClass) continue;

          const key = `${entityId as string}:${componentId as string}`;
          const worker = new workerClass();
          worker.start(this.nc, entityId);
          this.activeWorkers.set(key, worker);
        } catch (e) {
          const message = e instanceof Error ? e.message : String(e);
          console.error(`Failed to start worker: ${message}`);
        }
      }
    })();
  }

  private handleStopWorker(): void {
    const sub = this.nc.subscribe(Subjects.stopWorker);
    (async () => {
      for await (const msg of sub) {
        try {
          const { entityId, componentId } = JSON.parse(sc.decode(msg.data)) as {
            entityId: EntityId;
            componentId: ComponentId;
          };
          if (!this.workerClasses.has(componentId as string)) continue;

          const key = `${entityId as string}:${componentId as string}`;
          const worker = this.activeWorkers.get(key);
          if (worker) {
            worker.stop();
            this.activeWorkers.delete(key);
          }
        } catch (e) {
          const message = e instanceof Error ? e.message : String(e);
          console.error(`Failed to stop worker: ${message}`);
        }
      }
    })();
  }
}
