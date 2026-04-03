import type { NatsConnection } from "nats";
import { StringCodec } from "nats";
import type { EntityId, ComponentId } from "@engine/core";
import { Subjects, getAllComposites } from "@engine/core";
import type { ComponentWorkerClass, ComponentWorker } from "@engine/module";
import { getWorkerComponent } from "@engine/module";
import { EntityRepository } from "./entity-repository.js";

const sc = StringCodec();

interface RegisteredWorker {
  readonly workerClass: ComponentWorkerClass;
  readonly compositeIds: ComponentId[];
}

export class EntityHandler {
  private readonly repo = new EntityRepository();
  private readonly workers = new Map<string, RegisteredWorker>();
  /** Live worker instances, keyed by "entityId:componentId" */
  private readonly activeWorkers = new Map<string, ComponentWorker>();

  constructor(private readonly nc: NatsConnection) {}

  registerWorker(workerClass: ComponentWorkerClass): void {
    const component = getWorkerComponent(workerClass);
    const composites = getAllComposites(component);
    this.workers.set(component.id as string, {
      workerClass,
      compositeIds: composites.map((c) => c.id),
    });
  }

  async listen(): Promise<void> {
    this.handleCreateEntity();
    this.handleDeleteEntity();
    this.handleHasEntity();
    this.handleListEntities();
    this.handleAddComponent();
    this.handleRemoveComponent();
    this.handleHasComponent();
    this.handleGetComponents();
  }

  private handleCreateEntity(): void {
    const sub = this.nc.subscribe(Subjects.createEntity);
    (async () => {
      for await (const msg of sub) {
        const id = this.repo.create();
        msg.respond(sc.encode(id));
      }
    })();
  }

  private handleDeleteEntity(): void {
    const sub = this.nc.subscribe(Subjects.deleteEntity);
    (async () => {
      for await (const msg of sub) {
        const id = sc.decode(msg.data) as EntityId;
        // Stop all workers for this entity
        const componentIds = this.repo.getComponentIds(id);
        for (const componentId of componentIds) {
          const key = `${id as string}:${componentId as string}`;
          const worker = this.activeWorkers.get(key);
          if (worker) {
            worker.stop();
            this.activeWorkers.delete(key);
          }
        }
        const result = this.repo.delete(id);
        msg.respond(sc.encode(String(result)));
      }
    })();
  }

  private handleHasEntity(): void {
    const sub = this.nc.subscribe(Subjects.hasEntity);
    (async () => {
      for await (const msg of sub) {
        const id = sc.decode(msg.data) as EntityId;
        const result = this.repo.has(id);
        msg.respond(sc.encode(String(result)));
      }
    })();
  }

  private handleListEntities(): void {
    const sub = this.nc.subscribe(Subjects.listEntities);
    (async () => {
      for await (const msg of sub) {
        const ids = [...this.repo.getAll()];
        msg.respond(sc.encode(JSON.stringify(ids)));
      }
    })();
  }

  private handleAddComponent(): void {
    const sub = this.nc.subscribe(Subjects.addComponent);
    (async () => {
      for await (const msg of sub) {
        try {
          const { entityId, componentId } = JSON.parse(
            sc.decode(msg.data),
          ) as {
            entityId: EntityId;
            componentId: ComponentId;
          };
          const registered = this.workers.get(componentId as string);
          if (!registered) {
            msg.respond(
              sc.encode(
                JSON.stringify({
                  error: `No worker registered for component ${componentId as string}`,
                }),
              ),
            );
            continue;
          }

          // Record structure
          this.repo.addComponent(
            entityId,
            componentId,
            registered.compositeIds,
          );

          // Create and start worker — it subscribes to its own topics
          const worker = new registered.workerClass();
          worker.start(this.nc, entityId);
          this.activeWorkers.set(
            `${entityId as string}:${componentId as string}`,
            worker,
          );

          msg.respond(sc.encode(JSON.stringify({ ok: true })));
        } catch (e) {
          const message = e instanceof Error ? e.message : String(e);
          msg.respond(sc.encode(JSON.stringify({ error: message })));
        }
      }
    })();
  }

  private handleRemoveComponent(): void {
    const sub = this.nc.subscribe(Subjects.removeComponent);
    (async () => {
      for await (const msg of sub) {
        try {
          const { entityId, componentId } = JSON.parse(
            sc.decode(msg.data),
          ) as {
            entityId: EntityId;
            componentId: ComponentId;
          };
          const registered = this.workers.get(componentId as string);
          const compositeIds = registered ? registered.compositeIds : [];
          const result = this.repo.removeComponent(
            entityId,
            componentId,
            compositeIds,
          );

          // Stop worker — it unsubscribes from its own topics
          const key = `${entityId as string}:${componentId as string}`;
          const worker = this.activeWorkers.get(key);
          if (worker) {
            worker.stop();
            this.activeWorkers.delete(key);
          }

          msg.respond(sc.encode(String(result)));
        } catch (e) {
          const message = e instanceof Error ? e.message : String(e);
          msg.respond(sc.encode(JSON.stringify({ error: message })));
        }
      }
    })();
  }

  private handleHasComponent(): void {
    const sub = this.nc.subscribe(Subjects.hasComponent);
    (async () => {
      for await (const msg of sub) {
        const { entityId, componentId } = JSON.parse(
          sc.decode(msg.data),
        ) as {
          entityId: EntityId;
          componentId: ComponentId;
        };
        const result = this.repo.hasComponent(entityId, componentId);
        msg.respond(sc.encode(String(result)));
      }
    })();
  }

  private handleGetComponents(): void {
    const sub = this.nc.subscribe(Subjects.getComponents);
    (async () => {
      for await (const msg of sub) {
        try {
          const entityId = sc.decode(msg.data) as EntityId;
          const componentIds = this.repo.getComponentIds(entityId);
          msg.respond(
            sc.encode(
              JSON.stringify(
                componentIds.map((id) => ({ componentId: id as string })),
              ),
            ),
          );
        } catch (e) {
          const message = e instanceof Error ? e.message : String(e);
          msg.respond(sc.encode(JSON.stringify({ error: message })));
        }
      }
    })();
  }
}
