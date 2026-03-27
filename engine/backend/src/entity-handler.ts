import type { NatsConnection } from "nats";
import { StringCodec } from "nats";
import type { EntityId } from "@engine/core";
import { Subjects } from "@engine/core";
import { EntityRepository } from "./entity-repository.js";

const sc = StringCodec();

export class EntityHandler {
  private readonly repo = new EntityRepository();

  constructor(private readonly nc: NatsConnection) {}

  async listen(): Promise<void> {
    this.handleCreateEntity();
    this.handleDeleteEntity();
    this.handleHasEntity();
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
}
