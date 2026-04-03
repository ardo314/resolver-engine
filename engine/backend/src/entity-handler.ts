import type { NatsConnection } from "nats";
import { StringCodec } from "nats";
import type { EntityId, ComponentId } from "@engine/core";
import { Subjects, getAllComposites, getAllProperties } from "@engine/core";
import type { ComponentWorkerClass } from "@engine/module";
import { getWorkerComponent, createWorkerAccessors } from "@engine/module";
import { EntityRepository } from "./entity-repository.js";

const sc = StringCodec();

interface RegisteredWorker {
  readonly workerClass: ComponentWorkerClass;
  readonly compositeIds: ComponentId[];
  readonly propertyNames: string[];
}

export class EntityHandler {
  private readonly repo = new EntityRepository();
  private readonly workers = new Map<string, RegisteredWorker>();

  constructor(private readonly nc: NatsConnection) {}

  registerWorker(workerClass: ComponentWorkerClass): void {
    const component = getWorkerComponent(workerClass);
    const composites = getAllComposites(component);
    const properties = getAllProperties(component);
    this.workers.set(component.id as string, {
      workerClass,
      compositeIds: composites.map((c) => c.id),
      propertyNames: Object.keys(properties),
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
    this.handleGetProperty();
    this.handleSetProperty();
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
          const worker = this.workers.get(componentId as string);
          if (!worker) {
            msg.respond(
              sc.encode(
                JSON.stringify({
                  error: `No worker registered for component ${componentId as string}`,
                }),
              ),
            );
            continue;
          }
          const { accessors } = createWorkerAccessors(worker.workerClass);
          this.repo.addComponent(
            entityId,
            componentId,
            worker.compositeIds,
            accessors,
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
          const worker = this.workers.get(componentId as string);
          const compositeIds = worker ? worker.compositeIds : [];
          const result = this.repo.removeComponent(
            entityId,
            componentId,
            compositeIds,
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
          const components = [];
          for (const componentId of componentIds) {
            const worker = this.workers.get(componentId as string);
            const propertyNames = worker ? worker.propertyNames : [];
            const instance = this.repo.getWorkerInstance(
              entityId,
              componentId,
            ) as Record<string, { get(): Promise<unknown> }> | undefined;
            const properties = [];
            for (const name of propertyNames) {
              let value: unknown = null;
              if (instance?.[name]) {
                value = await instance[name].get();
              }
              properties.push({ name, value: JSON.stringify(value) });
            }
            components.push({
              componentId: componentId as string,
              properties,
            });
          }
          msg.respond(sc.encode(JSON.stringify(components)));
        } catch (e) {
          const message = e instanceof Error ? e.message : String(e);
          msg.respond(sc.encode(JSON.stringify({ error: message })));
        }
      }
    })();
  }

  private handleGetProperty(): void {
    const sub = this.nc.subscribe(Subjects.getProperty);
    (async () => {
      for await (const msg of sub) {
        try {
          const { entityId, componentId, property } = JSON.parse(
            sc.decode(msg.data),
          ) as {
            entityId: EntityId;
            componentId: ComponentId;
            property: string;
          };
          const instance = this.repo.getWorkerInstance(
            entityId,
            componentId,
          ) as Record<string, { get(): Promise<unknown> }> | undefined;
          if (!instance || !instance[property]) {
            msg.respond(
              sc.encode(
                JSON.stringify({ error: `Property ${property} not found` }),
              ),
            );
            continue;
          }
          const value = await instance[property].get();
          msg.respond(sc.encode(JSON.stringify({ value })));
        } catch (e) {
          const message = e instanceof Error ? e.message : String(e);
          msg.respond(sc.encode(JSON.stringify({ error: message })));
        }
      }
    })();
  }

  private handleSetProperty(): void {
    const sub = this.nc.subscribe(Subjects.setProperty);
    (async () => {
      for await (const msg of sub) {
        try {
          const { entityId, componentId, property, value } = JSON.parse(
            sc.decode(msg.data),
          ) as {
            entityId: EntityId;
            componentId: ComponentId;
            property: string;
            value: unknown;
          };
          const instance = this.repo.getWorkerInstance(
            entityId,
            componentId,
          ) as Record<string, { set(v: unknown): Promise<void> }> | undefined;
          if (!instance || !instance[property]) {
            msg.respond(
              sc.encode(
                JSON.stringify({ error: `Property ${property} not found` }),
              ),
            );
            continue;
          }
          await instance[property].set(value);
          msg.respond(sc.encode(JSON.stringify({ ok: true })));
        } catch (e) {
          const message = e instanceof Error ? e.message : String(e);
          msg.respond(sc.encode(JSON.stringify({ error: message })));
        }
      }
    })();
  }
}
