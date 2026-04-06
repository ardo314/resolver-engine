import type { NatsConnection } from "nats";
import { StringCodec } from "nats";
import type { ComponentReference, EntityId } from "@engine/core";
import {
  Subjects,
  WorkerSubjects,
  type Component,
  getAllProperties,
  getAllMethods,
} from "@engine/core";

const sc = StringCodec();

function createRemoteComponentReference<C extends Component>(
  nc: NatsConnection,
  entityId: EntityId,
  component: C,
): ComponentReference<C> {
  const proxy: Record<string, unknown> = {};
  const allProps = getAllProperties(component);
  for (const key of Object.keys(allProps)) {
    proxy[key] = {
      async get() {
        const reply = await nc.request(
          WorkerSubjects.getProperty(
            component.id as string,
            entityId as string,
            key,
          ),
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
            key,
          ),
          sc.encode(JSON.stringify({ value })),
        );
        const result = JSON.parse(sc.decode(reply.data)) as {
          ok?: boolean;
          error?: string;
        };
        if (result.error) throw new Error(result.error);
      },
    };
  }
  const allMethodDefs = getAllMethods(component);
  for (const name of Object.keys(allMethodDefs)) {
    proxy[name] = async (input?: unknown) => {
      const reply = await nc.request(
        WorkerSubjects.callMethod(
          component.id as string,
          entityId as string,
          name,
        ),
        input !== undefined ? sc.encode(JSON.stringify({ input })) : undefined,
      );
      const result = JSON.parse(sc.decode(reply.data)) as {
        result?: unknown;
        error?: string;
      };
      if (result.error) throw new Error(result.error);
      return result.result;
    };
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
