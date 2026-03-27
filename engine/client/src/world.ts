import type { NatsConnection } from "nats";
import { StringCodec } from "nats";
import type { EntityId } from "@engine/core";
import { Subjects } from "@engine/core";
import { Entity } from "./entity.js";

const sc = StringCodec();

export class World {
  constructor(private readonly nc: NatsConnection) {}

  async createEntity(): Promise<Entity> {
    const reply = await this.nc.request(Subjects.createEntity, sc.encode(""));
    const id = sc.decode(reply.data) as EntityId;
    return new Entity(this.nc, id);
  }

  async deleteEntity(id: EntityId): Promise<boolean> {
    const reply = await this.nc.request(Subjects.deleteEntity, sc.encode(id));
    return sc.decode(reply.data) === "true";
  }

  async hasEntity(id: EntityId): Promise<boolean> {
    const reply = await this.nc.request(Subjects.hasEntity, sc.encode(id));
    return sc.decode(reply.data) === "true";
  }
}
