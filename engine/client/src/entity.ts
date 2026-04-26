import type { NatsConnection } from "nats";
import { StringCodec } from "nats";
import type { ComponentReference, EntityId } from "@engine/core";
import {
  Subjects,
  WorkerSubjects,
  type Component,
  type Query,
  type QueryReference,
} from "@engine/core";

const sc = StringCodec();

function createRemoteComponentReference<C extends Component>(
  nc: NatsConnection,
  entityId: EntityId,
  component: C,
): ComponentReference<C> {
  const proxy: Record<string, unknown> = {};
  for (const method of component.methods) {
    proxy[method.name] = async (input?: unknown) => {
      const reply = await nc.request(
        WorkerSubjects.callMethod(
          component.id as string,
          entityId as string,
          method.name,
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

function createRemoteQueryReference<Q extends Query>(
  nc: NatsConnection,
  entityId: EntityId,
  query: Q,
  methodMap: Record<string, string>,
): QueryReference<Q> {
  const proxy: Record<string, unknown> = {};
  for (const method of query.methods) {
    const componentId = methodMap[method.name];
    proxy[method.name] = async (input?: unknown) => {
      const reply = await nc.request(
        WorkerSubjects.callMethod(
          componentId,
          entityId as string,
          method.name,
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
  return proxy as QueryReference<Q>;
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

  async query<Q extends Query>(
    query: Q,
  ): Promise<QueryReference<Q> | null> {
    const methodNames = query.methods.map((m) => m.name);
    const reply = await this.nc.request(
      Subjects.queryEntity,
      sc.encode(
        JSON.stringify({ entityId: this.id, methodNames }),
      ),
    );
    const result = JSON.parse(sc.decode(reply.data)) as {
      match: boolean;
      methods?: Record<string, string>;
    };
    if (!result.match) return null;
    return createRemoteQueryReference(this.nc, this.id, query, result.methods!);
  }

  async getComponentEntries(): Promise<
    {
      componentId: string;
      methodNames: string[];
    }[]
  > {
    const reply = await this.nc.request(
      Subjects.getComponents,
      sc.encode(this.id),
    );
    return JSON.parse(sc.decode(reply.data)) as {
      componentId: string;
      methodNames: string[];
    }[];
  }
}
