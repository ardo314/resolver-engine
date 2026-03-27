import type { NatsConnection } from "nats";
import { StringCodec } from "nats";
import type { EntityId } from "@engine/core";
import { Subjects } from "@engine/core";

const sc = StringCodec();

export class Entity {
  constructor(
    private readonly nc: NatsConnection,
    public readonly id: EntityId,
  ) {}

  async addComponent(componentId: string): Promise<void> {
    await this.nc.request(
      Subjects.addComponent,
      sc.encode(JSON.stringify({ entityId: this.id, componentId })),
    );
  }

  async removeComponent(componentId: string): Promise<void> {
    await this.nc.request(
      Subjects.removeComponent,
      sc.encode(JSON.stringify({ entityId: this.id, componentId })),
    );
  }

  async hasComponent(componentId: string): Promise<boolean> {
    const reply = await this.nc.request(
      Subjects.hasComponent,
      sc.encode(JSON.stringify({ entityId: this.id, componentId })),
    );
    return sc.decode(reply.data) === "true";
  }
}
