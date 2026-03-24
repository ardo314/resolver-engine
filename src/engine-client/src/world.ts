import { EntityId } from "engine-core";
import { NatsConnection, StringCodec, Empty } from "nats";

const sc = StringCodec();

/**
 * Client-side proxy to the backend EntityService.
 * All operations are forwarded over NATS request-reply.
 */
export class World {
  private readonly nats: NatsConnection;

  constructor(nats: NatsConnection) {
    this.nats = nats;
  }

  /**
   * Creates a new entity on the backend and returns a local Entity handle.
   */
  async createEntityAsync(): Promise<{ id: EntityId }> {
    const reply = await this.nats.request("entity.create", Empty);
    const data = sc.decode(reply.data);
    const id = parseEntityId(data, "create");
    return { id };
  }

  /**
   * Destroys an entity on the backend.
   */
  async destroyEntityAsync(id: EntityId): Promise<void> {
    const reply = await this.nats.request(
      "entity.destroy",
      sc.encode(id.value),
    );
    const data = sc.decode(reply.data);
    if (data !== "ok") {
      throw new Error(`Failed to destroy entity ${id}: ${data}`);
    }
  }

  /**
   * Returns whether the given entity exists on the backend.
   */
  async entityExistsAsync(id: EntityId): Promise<boolean> {
    const reply = await this.nats.request("entity.exists", sc.encode(id.value));
    return sc.decode(reply.data) === "true";
  }

  /**
   * Lists all entity IDs known to the backend.
   */
  async listEntitiesAsync(): Promise<EntityId[]> {
    const reply = await this.nats.request("entity.list", Empty);
    const data = sc.decode(reply.data);
    if (!data) return [];
    return data.split(",").map((s) => new EntityId(s));
  }
}

function parseEntityId(data: string, operation: string): EntityId {
  if (!data) {
    throw new Error(
      `Backend returned invalid EntityId from ${operation}: ${data}`,
    );
  }
  return new EntityId(data);
}
