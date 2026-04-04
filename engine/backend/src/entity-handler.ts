import type { NatsConnection } from "nats";
import { StringCodec } from "nats";
import type { EntityId, ComponentId } from "@engine/core";
import { Subjects } from "@engine/core";
import { EntityRepository } from "./entity-repository.js";

const sc = StringCodec();

interface RegisteredComponent {
  readonly compositeIds: ComponentId[];
}

export class EntityHandler {
  private readonly repo = new EntityRepository();
  private readonly components = new Map<string, RegisteredComponent>();

  constructor(private readonly nc: NatsConnection) {}

  async listen(): Promise<void> {
    this.handleRegisterComponent();
    this.handleCreateEntity();
    this.handleDeleteEntity();
    this.handleHasEntity();
    this.handleListEntities();
    this.handleAddComponent();
    this.handleRemoveComponent();
    this.handleHasComponent();
    this.handleGetComponents();
  }

  private handleRegisterComponent(): void {
    const sub = this.nc.subscribe(Subjects.registerComponent);
    (async () => {
      for await (const msg of sub) {
        try {
          const { componentId, compositeIds } = JSON.parse(
            sc.decode(msg.data),
          ) as {
            componentId: string;
            compositeIds: string[];
          };
          this.components.set(componentId, {
            compositeIds: compositeIds as ComponentId[],
          });
          msg.respond(sc.encode(JSON.stringify({ ok: true })));
        } catch (e) {
          const message = e instanceof Error ? e.message : String(e);
          msg.respond(sc.encode(JSON.stringify({ error: message })));
        }
      }
    })();
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
        // Publish stop events for all workers on this entity
        const componentIds = this.repo.getComponentIds(id);
        for (const componentId of componentIds) {
          this.nc.publish(
            Subjects.stopWorker,
            sc.encode(JSON.stringify({ entityId: id, componentId })),
          );
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
          const { entityId, componentId } = JSON.parse(sc.decode(msg.data)) as {
            entityId: EntityId;
            componentId: ComponentId;
          };
          const registered = this.components.get(componentId as string);
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

          // Publish start event — worker container will handle it
          this.nc.publish(
            Subjects.startWorker,
            sc.encode(JSON.stringify({ entityId, componentId })),
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
          const { entityId, componentId } = JSON.parse(sc.decode(msg.data)) as {
            entityId: EntityId;
            componentId: ComponentId;
          };
          const registered = this.components.get(componentId as string);
          const compositeIds = registered ? registered.compositeIds : [];
          const result = this.repo.removeComponent(
            entityId,
            componentId,
            compositeIds,
          );

          // Publish stop event — worker container will handle it
          this.nc.publish(
            Subjects.stopWorker,
            sc.encode(JSON.stringify({ entityId, componentId })),
          );

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
        const { entityId, componentId } = JSON.parse(sc.decode(msg.data)) as {
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
