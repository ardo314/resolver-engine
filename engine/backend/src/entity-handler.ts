import type { NatsConnection } from "nats";
import { StringCodec } from "nats";
import type { EntityId, ComponentId, ComponentSchema } from "@engine/core";
import { Subjects } from "@engine/core";
import { EntityRepository } from "./entity-repository.js";

const sc = StringCodec();

interface RegisteredComponent {
  readonly methodNames: string[];
  readonly schema: ComponentSchema;
}

export class EntityHandler {
  private readonly repo = new EntityRepository();
  private readonly components = new Map<string, RegisteredComponent>();

  constructor(private readonly nc: NatsConnection) {}

  async listen(): Promise<void> {
    this.handleRegisterComponent();
    this.handleListComponents();
    this.handleCreateEntity();
    this.handleDeleteEntity();
    this.handleHasEntity();
    this.handleListEntities();
    this.handleAddComponent();
    this.handleRemoveComponent();
    this.handleHasComponent();
    this.handleGetComponents();
    this.handleQueryEntity();
  }

  private handleRegisterComponent(): void {
    const sub = this.nc.subscribe(Subjects.registerComponent);
    (async () => {
      for await (const msg of sub) {
        try {
          const { componentId, methodNames, schema } = JSON.parse(
            sc.decode(msg.data),
          ) as {
            componentId: string;
            methodNames: string[];
            schema: ComponentSchema;
          };
          this.components.set(componentId, {
            methodNames,
            schema,
          });
          msg.respond(sc.encode(JSON.stringify({ ok: true })));
        } catch (e) {
          const message = e instanceof Error ? e.message : String(e);
          msg.respond(sc.encode(JSON.stringify({ error: message })));
        }
      }
    })();
  }

  private handleListComponents(): void {
    const sub = this.nc.subscribe(Subjects.listComponents);
    (async () => {
      for await (const msg of sub) {
        const entries = [...this.components.entries()].map(
          ([componentId, { methodNames, schema }]) => ({
            componentId,
            methodNames,
            schema,
          }),
        );
        msg.respond(sc.encode(JSON.stringify(entries)));
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

          this.repo.addComponent(entityId, componentId);

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
          const result = this.repo.removeComponent(entityId, componentId);

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
                componentIds.map((id) => {
                  const registered = this.components.get(id as string);
                  return {
                    componentId: id as string,
                    methodNames: registered ? registered.methodNames : [],
                  };
                }),
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

  private handleQueryEntity(): void {
    const sub = this.nc.subscribe(Subjects.queryEntity);
    (async () => {
      for await (const msg of sub) {
        try {
          const { entityId, methodNames } = JSON.parse(
            sc.decode(msg.data),
          ) as {
            entityId: EntityId;
            methodNames: string[];
          };

          const componentIds = this.repo.getComponentIds(entityId);

          // Build method → componentId mapping across all components on the entity
          const methodMap: Record<string, string> = {};
          for (const compId of componentIds) {
            const registered = this.components.get(compId as string);
            if (!registered) continue;
            for (const methodName of registered.methodNames) {
              // First component wins for a given method name
              if (!(methodName in methodMap)) {
                methodMap[methodName] = compId as string;
              }
            }
          }

          // Check if all requested methods are covered
          const missing = methodNames.filter((m) => !(m in methodMap));
          if (missing.length > 0) {
            msg.respond(
              sc.encode(JSON.stringify({ match: false, missing })),
            );
          } else {
            // Return only the requested methods' mapping
            const methods: Record<string, string> = {};
            for (const m of methodNames) {
              methods[m] = methodMap[m];
            }
            msg.respond(
              sc.encode(JSON.stringify({ match: true, methods })),
            );
          }
        } catch (e) {
          const message = e instanceof Error ? e.message : String(e);
          msg.respond(sc.encode(JSON.stringify({ error: message })));
        }
      }
    })();
  }
}
