import type { NatsConnection } from "nats";
import { StringCodec } from "nats";
import type {
  ComponentProxy,
  EntityId,
  SchemaProxy,
  SchemaId,
} from "@engine/core";
import { Subjects, Component, Schema, isComponent } from "@engine/core";

const sc = StringCodec();

function createRemoteSchemaProxy<S extends Schema>(
  nc: NatsConnection,
  entityId: EntityId,
  schema: S,
): SchemaProxy<S> {
  const proxy: Record<
    string,
    { get(): Promise<unknown>; set(v: unknown): Promise<void> }
  > = {};
  const props = schema.definition.properties;
  if (props) {
    for (const key of Object.keys(props)) {
      proxy[key] = {
        async get() {
          const reply = await nc.request(
            Subjects.getProperty,
            sc.encode(
              JSON.stringify({ entityId, schemaId: schema.id, property: key }),
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
            Subjects.setProperty,
            sc.encode(
              JSON.stringify({
                entityId,
                schemaId: schema.id,
                property: key,
                value,
              }),
            ),
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
  return proxy as SchemaProxy<S>;
}

function createRemoteComponentProxy<C extends Component>(
  nc: NatsConnection,
  entityId: EntityId,
  component: C,
): ComponentProxy<C> {
  const merged: Record<string, unknown> = {};
  for (const schema of component.schemas) {
    const schemaProxy = createRemoteSchemaProxy(nc, entityId, schema);
    Object.assign(merged, schemaProxy);
  }
  return merged as ComponentProxy<C>;
}

export class Entity {
  constructor(
    private readonly nc: NatsConnection,
    public readonly id: EntityId,
  ) {}

  async addComponent<C extends Component>(
    component: C,
  ): Promise<ComponentProxy<C>> {
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
    return createRemoteComponentProxy(this.nc, this.id, component);
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
  ): Promise<ComponentProxy<C> | null>;
  async getComponent<S extends Schema>(
    schema: S,
  ): Promise<SchemaProxy<S> | null>;
  async getComponent(arg: Component | Schema): Promise<unknown> {
    if (isComponent(arg)) {
      const has = await this.hasComponent(arg);
      if (!has) return null;
      return createRemoteComponentProxy(this.nc, this.id, arg);
    }
    // Schema path — check if entity has this schema via hasComponent on a
    // synthetic single-schema component (schemaId == componentId lookup).
    // We use getProperty with a probe to check existence; alternatively we
    // could add a hasSchema subject. For now, try to build the proxy and
    // return null on error.
    const schema = arg as Schema;
    const reply = await this.nc.request(
      Subjects.hasComponent,
      sc.encode(JSON.stringify({ entityId: this.id, componentId: schema.id })),
    );
    // For single-schema components, componentId == schemaId.
    // For multi-schema components, we need a different check. We'll iterate
    // any known schemas. As a pragmatic approach: create a proxy and if the
    // first property get fails, the schema isn't present.
    // Better: let's use the property to probe existence.
    const props = schema.definition.properties;
    if (props) {
      const firstKey = Object.keys(props)[0];
      if (firstKey) {
        try {
          const probeReply = await this.nc.request(
            Subjects.getProperty,
            sc.encode(
              JSON.stringify({
                entityId: this.id,
                schemaId: schema.id,
                property: firstKey,
              }),
            ),
          );
          const result = JSON.parse(sc.decode(probeReply.data)) as {
            value?: unknown;
            error?: string;
          };
          if (result.error) return null;
        } catch {
          return null;
        }
      }
    }
    return createRemoteSchemaProxy(this.nc, this.id, schema);
  }

  async getComponentEntries(): Promise<
    {
      componentId: string;
      schemas: {
        schemaId: string;
        properties: { name: string; value: string }[];
      }[];
    }[]
  > {
    const reply = await this.nc.request(
      Subjects.getComponents,
      sc.encode(this.id),
    );
    return JSON.parse(sc.decode(reply.data));
  }
}
