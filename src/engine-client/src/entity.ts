import { EntityId } from "engine-core";
import { NatsConnection, StringCodec, JSONCodec } from "nats";

const sc = StringCodec();

/**
 * Client-side proxy for a single entity.
 * Component operations are forwarded to the backend over NATS request-reply.
 */
export class Entity {
  readonly id: EntityId;
  readonly nats: NatsConnection;

  constructor(id: EntityId, nats: NatsConnection) {
    this.id = id;
    this.nats = nats;
  }

  /**
   * Registers a component on this entity via the backend.
   */
  async addComponentAsync(componentName: string): Promise<void> {
    const payload = `${this.id.value}:${componentName}`;
    const reply = await this.nats.request(
      "entity.add-component",
      sc.encode(payload),
    );
    const data = sc.decode(reply.data);
    if (data !== "ok") {
      throw new Error(
        `Failed to add component ${componentName} to entity ${this.id}: ${data}`,
      );
    }
  }

  /**
   * Removes a component from this entity via the backend.
   */
  async removeComponentAsync(componentName: string): Promise<void> {
    const payload = `${this.id.value}:${componentName}`;
    const reply = await this.nats.request(
      "entity.remove-component",
      sc.encode(payload),
    );
    const data = sc.decode(reply.data);
    if (data !== "ok") {
      throw new Error(
        `Failed to remove component ${componentName} from entity ${this.id}: ${data}`,
      );
    }
  }

  /**
   * Checks whether this entity has a given component via the backend.
   */
  async hasComponentAsync(componentName: string): Promise<boolean> {
    const payload = `${this.id.value}:${componentName}`;
    const reply = await this.nats.request(
      "entity.has-component",
      sc.encode(payload),
    );
    return sc.decode(reply.data) === "true";
  }

  /**
   * Lists all component names attached to this entity.
   */
  async listComponentsAsync(): Promise<string[]> {
    const reply = await this.nats.request(
      "entity.list-components",
      sc.encode(this.id.value),
    );
    const data = sc.decode(reply.data);
    if (!data) return [];
    return data.split(",");
  }
}
