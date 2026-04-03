import type { NatsConnection } from "nats";
import { StringCodec } from "nats";
import type { ComponentReference, EntityId } from "@engine/core";
import {
  Subjects,
  WorkerSubjects,
  type Component,
  getAllProperties,
} from "@engine/core";

const sc = StringCodec();

function createRemoteComponentReference<C extends Component>(
  nc: NatsConnection,
  entityId: EntityId,
  component: C,
): ComponentReference<C> {
  const proxy: Record<
    string,
    { get(): Promise<unknown>; set(v: unknown): Promise<void> }
  > = {};
  const allProps = getAllProperties(component);
  for (const key of Object.keys(allProps)) {
    proxy[key] = {
      async get() {
        const reply = await nc.request(
          WorkerSubjects.getProperty(
            component.id as string,
            entityId as string,
          ),
          sc.encode(JSON.stringify({ property: key })),
        );
        const result = JSON.parse(sc.decode(reply.data)) as {
          value?: unknown;
          error?: string;
        };
        if (result.error) throw new Error(result.error);
        return result.value;
      },
      async set(value: unknown) {
        const reply = await nc.request(
          WorkerSubjects.setProperty(
            component.id as string,
            entityId as string,
          ),
          sc.encode(JSON.stringify({ property: key, value })),
        );
        const result = JSON.parse(sc.decode(reply.data)) as {
          ok?: boolean;
          error?: string;
        };
        if (result.error) throw new Error(result.error);
      },
    };
  }
  return proxy as ComponentReference<C>;
}

function createScopedComponentReference<C extends Component>(
  nc: NatsConnection,
  entityId: EntityId,
  component: C,
  scopedComponent: Component,
): ComponentReference<C> {
  const proxy: Record<
    string,
    { get(): Promise<unknown>; set(v: unknown): Promise<void> }
  > = {};
  const ownProps = scopedComponent.definition.properties;
  if (ownProps) {
    for (const key of Object.keys(ownProps)) {
      proxy[key] = {
        async get() {
          const reply = await nc.request(
            WorkerSubjects.getProperty(
              component.id as string,
              entityId as string,
            ),
            sc.encode(JSON.stringify({ property: key })),
          );
          const result = JSON.parse(sc.decode(reply.data)) as {
            value?: unknown;
            error?: string;
          };
          if (result.error) throw new Error(result.error);
          return result.value;
        },
        async set(value: unknown) {
          const reply = await nc.request(
            WorkerSubjects.setProperty(
              component.id as string,
              entityId as string,
            ),
            sc.encode(JSON.stringify({ property: key, value })),
          );
          const result = JSON.parse(sc.decode(reply.data)) as {
            ok?: boolean;
            error?: string;
          };
          if (result.error) throw new Error(result.error);
        },
      };
    }
  }
  return proxy as ComponentReference<C>;
}

export class Entity {
  constructor(
    private readonly nc: NatsConnection,
    public readonly id: EntityId,
  ) {}

  async addComponent<C extends Component>(
    component: C,
  ): Promise<ComponentReference<C>> {
    const reply = await this.nc.request(
      Subjects.addComponent,
      sc.encode(
        JSON.stringify({ entityId: this.id, componentId: component.id }),
      ),
    );
    const result = JSON.parse(sc.decode(reply.data)) as {
      ok?: boolean;
      error?: string;
    };
    if (result.error) throw new Error(result.error);
    return createRemoteComponentReference(this.nc, this.id, component);
  }

  async removeComponent<C extends Component>(component: C): Promise<void> {
    await this.nc.request(
      Subjects.removeComponent,
      sc.encode(
        JSON.stringify({ entityId: this.id, componentId: component.id }),
      ),
    );
  }

  async hasComponent<C extends Component>(component: C): Promise<boolean> {
    const reply = await this.nc.request(
      Subjects.hasComponent,
      sc.encode(
        JSON.stringify({ entityId: this.id, componentId: component.id }),
      ),
    );
    return sc.decode(reply.data) === "true";
  }

  async getComponent<C extends Component>(
    component: C,
  ): Promise<ComponentReference<C> | null> {
    const has = await this.hasComponent(component);
    if (!has) return null;
    return createRemoteComponentReference(this.nc, this.id, component);
  }

  async getComponentEntries(): Promise<
    {
      componentId: string;
      properties: { name: string; value: string }[];
    }[]
  > {
    const reply = await this.nc.request(
      Subjects.getComponents,
      sc.encode(this.id),
    );
    const structural = JSON.parse(sc.decode(reply.data)) as {
      componentId: string;
    }[];

    const result: {
      componentId: string;
      properties: { name: string; value: string }[];
    }[] = [];

    for (const { componentId } of structural) {
      result.push({ componentId, properties: [] });
    }
    return result;
  }
}
