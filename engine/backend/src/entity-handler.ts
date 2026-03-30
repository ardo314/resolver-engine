import type { NatsConnection } from "nats";
import { StringCodec } from "nats";
import type { EntityId, ComponentId, SchemaId } from "@engine/core";
import { Subjects } from "@engine/core";
import type { ComponentWorker } from "@engine/module";
import { EntityRepository } from "./entity-repository.js";

const sc = StringCodec();

export class EntityHandler {
  private readonly repo = new EntityRepository();
  private readonly workers = new Map<string, ComponentWorker>();

  constructor(private readonly nc: NatsConnection) {}

  registerWorker(worker: ComponentWorker): void {
    this.workers.set(worker.component.id as string, worker);
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
          const schemaIds = worker.component.schemas.map((s) => s.id);
          const instance = worker.create();
          this.repo.addComponent(entityId, componentId, schemaIds, instance);
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
          const schemaIds = worker
            ? worker.component.schemas.map((s) => s.id)
            : [];
          const result = this.repo.removeComponent(
            entityId,
            componentId,
            schemaIds,
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
            const schemaIds = this.repo.getSchemaIdsForComponent(
              entityId,
              componentId,
            );
            const worker = this.workers.get(componentId as string);
            const schemas = [];
            for (const schemaId of schemaIds) {
              const schemaDef = worker?.component.schemas.find(
                (s) => s.id === schemaId,
              );
              const propertyNames = schemaDef?.definition.properties
                ? Object.keys(schemaDef.definition.properties)
                : [];
              const properties = [];
              for (const name of propertyNames) {
                const instance = this.repo.getWorkerInstanceBySchema(
                  entityId,
                  schemaId,
                ) as Record<string, { get(): Promise<unknown> }> | undefined;
                let value: unknown = null;
                if (instance?.[name]) {
                  value = await instance[name].get();
                }
                properties.push({ name, value: JSON.stringify(value) });
              }
              schemas.push({
                schemaId: schemaId as string,
                properties,
              });
            }
            components.push({
              componentId: componentId as string,
              schemas,
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
          const { entityId, schemaId, property } = JSON.parse(
            sc.decode(msg.data),
          ) as {
            entityId: EntityId;
            schemaId: SchemaId;
            property: string;
          };
          const instance = this.repo.getWorkerInstanceBySchema(
            entityId,
            schemaId,
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
          const { entityId, schemaId, property, value } = JSON.parse(
            sc.decode(msg.data),
          ) as {
            entityId: EntityId;
            schemaId: SchemaId;
            property: string;
            value: unknown;
          };
          const instance = this.repo.getWorkerInstanceBySchema(
            entityId,
            schemaId,
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
