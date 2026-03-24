import { EntityId } from "engine-core";
import { NatsConnection, StringCodec, Subscription } from "nats";
import { EntityRepository } from "./entity-repository.js";

const sc = StringCodec();

/**
 * Manages entity lifecycles and component tracking, exposed as a NATS service.
 *
 * When adding or removing components, the backend first sends a NATS request to the
 * module runtime (worker.create / worker.remove) and only commits to the entity
 * registry if the runtime responds successfully.
 *
 * NATS subjects (under the "entity" service group):
 *   entity.create           – request with empty payload, replies with the new EntityId.
 *   entity.destroy          – request with EntityId string, replies with "ok" or an error.
 *   entity.exists           – request with EntityId string, replies with "true" / "false".
 *   entity.list             – request with empty payload, replies with comma-separated EntityId list.
 *   entity.add-component    – request "entityId:componentName", replies "ok" or error.
 *   entity.remove-component – request "entityId:componentName", replies "ok" or error.
 *   entity.has-component    – request "entityId:componentName", replies "true" / "false".
 *   entity.list-components  – request EntityId string, replies comma-separated names.
 */
export class EntityService {
  private readonly nats: NatsConnection;
  private readonly repo: EntityRepository;
  private readonly subscriptions: Subscription[] = [];

  constructor(nats: NatsConnection, repo: EntityRepository) {
    this.nats = nats;
    this.repo = repo;
  }

  /** Registers all NATS subscriptions and begins listening for requests. */
  async start(): Promise<void> {
    this.subscribe("entity.create", (_, msg) => this.handleCreate(msg));
    this.subscribe("entity.destroy", (data, msg) =>
      this.handleDestroy(data, msg),
    );
    this.subscribe("entity.exists", (data, msg) =>
      this.handleExists(data, msg),
    );
    this.subscribe("entity.list", (_, msg) => this.handleList(msg));
    this.subscribe("entity.add-component", (data, msg) =>
      this.handleAddComponent(data, msg),
    );
    this.subscribe("entity.remove-component", (data, msg) =>
      this.handleRemoveComponent(data, msg),
    );
    this.subscribe("entity.has-component", (data, msg) =>
      this.handleHasComponent(data, msg),
    );
    this.subscribe("entity.list-components", (data, msg) =>
      this.handleListComponents(data, msg),
    );
  }

  private subscribe(
    subject: string,
    handler: (
      data: string,
      msg: { respond: (data: Uint8Array) => boolean },
    ) => Promise<void>,
  ): void {
    const sub = this.nats.subscribe(subject);
    this.subscriptions.push(sub);
    (async () => {
      for await (const msg of sub) {
        try {
          const data = sc.decode(msg.data);
          await handler(data, msg);
        } catch (err) {
          console.error(`Error handling ${subject}:`, err);
          msg.respond(sc.encode(`error: ${err}`));
        }
      }
    })();
  }

  // ── Entity lifecycle ────────────────────────────────────────────────

  private async handleCreate(msg: {
    respond: (data: Uint8Array) => boolean;
  }): Promise<void> {
    const id = this.repo.create();
    msg.respond(sc.encode(id.value));
  }

  private async handleDestroy(
    data: string,
    msg: { respond: (data: Uint8Array) => boolean },
  ): Promise<void> {
    const id = new EntityId(data);
    const components = this.repo.destroy(id);

    if (components === null) {
      msg.respond(sc.encode("error: entity not found"));
      return;
    }

    // Tear down all workers that were instantiated for this entity.
    for (const componentName of components) {
      try {
        await this.nats.request(
          `worker.remove.${componentName}`,
          sc.encode(id.value),
          { timeout: 5000 },
        );
      } catch {
        // Module is gone – nothing to clean up on the runtime side.
      }
    }

    msg.respond(sc.encode("ok"));
  }

  private async handleExists(
    data: string,
    msg: { respond: (data: Uint8Array) => boolean },
  ): Promise<void> {
    const id = new EntityId(data);
    msg.respond(sc.encode(this.repo.exists(id) ? "true" : "false"));
  }

  private async handleList(msg: {
    respond: (data: Uint8Array) => boolean;
  }): Promise<void> {
    const ids = this.repo
      .listAll()
      .map((e) => e.value)
      .join(",");
    msg.respond(sc.encode(ids));
  }

  // ── Component management ────────────────────────────────────────────

  private async handleAddComponent(
    data: string,
    msg: { respond: (data: Uint8Array) => boolean },
  ): Promise<void> {
    const parsed = tryParseRequest(data);
    if (!parsed) {
      msg.respond(sc.encode("error: expected format entityId:componentName"));
      return;
    }

    const { entityId, componentName } = parsed;

    if (!this.repo.exists(entityId)) {
      msg.respond(sc.encode("error: entity not found"));
      return;
    }

    if (this.repo.hasComponent(entityId, componentName)) {
      msg.respond(sc.encode("error: component already added"));
      return;
    }

    // Request the module runtime to create a worker for this component.
    try {
      const reply = await this.nats.request(
        `worker.create.${componentName}`,
        sc.encode(entityId.value),
        { timeout: 5000 },
      );
      const replyData = sc.decode(reply.data);
      if (replyData !== "ok") {
        msg.respond(sc.encode(`error: module runtime error: ${replyData}`));
        return;
      }
    } catch {
      msg.respond(sc.encode("error: no module handles this component"));
      return;
    }

    if (!this.repo.addComponent(entityId, componentName)) {
      msg.respond(sc.encode("error: component already added"));
      return;
    }

    msg.respond(sc.encode("ok"));
  }

  private async handleRemoveComponent(
    data: string,
    msg: { respond: (data: Uint8Array) => boolean },
  ): Promise<void> {
    const parsed = tryParseRequest(data);
    if (!parsed) {
      msg.respond(sc.encode("error: expected format entityId:componentName"));
      return;
    }

    const { entityId, componentName } = parsed;

    if (!this.repo.exists(entityId)) {
      msg.respond(sc.encode("error: entity not found"));
      return;
    }

    if (!this.repo.hasComponent(entityId, componentName)) {
      msg.respond(sc.encode("error: component not found on entity"));
      return;
    }

    // Request the module runtime to destroy the worker for this component.
    try {
      const reply = await this.nats.request(
        `worker.remove.${componentName}`,
        sc.encode(entityId.value),
        { timeout: 5000 },
      );
      const replyData = sc.decode(reply.data);
      if (replyData !== "ok") {
        msg.respond(sc.encode(`error: module runtime error: ${replyData}`));
        return;
      }
    } catch {
      msg.respond(sc.encode("error: no module handles this component"));
      return;
    }

    if (!this.repo.removeComponent(entityId, componentName)) {
      msg.respond(sc.encode("error: component not found on entity"));
      return;
    }

    msg.respond(sc.encode("ok"));
  }

  private async handleHasComponent(
    data: string,
    msg: { respond: (data: Uint8Array) => boolean },
  ): Promise<void> {
    const parsed = tryParseRequest(data);
    if (!parsed) {
      msg.respond(sc.encode("error: expected format entityId:componentName"));
      return;
    }

    const { entityId, componentName } = parsed;

    if (!this.repo.exists(entityId)) {
      msg.respond(sc.encode("error: entity not found"));
      return;
    }

    msg.respond(
      sc.encode(
        this.repo.hasComponent(entityId, componentName) ? "true" : "false",
      ),
    );
  }

  private async handleListComponents(
    data: string,
    msg: { respond: (data: Uint8Array) => boolean },
  ): Promise<void> {
    const entityId = new EntityId(data);

    if (!this.repo.exists(entityId)) {
      msg.respond(sc.encode("error: entity not found"));
      return;
    }

    const names = this.repo.listComponents(entityId);
    msg.respond(sc.encode(names.join(",")));
  }

  /** Unsubscribe all listeners. */
  async stop(): Promise<void> {
    for (const sub of this.subscriptions) {
      sub.unsubscribe();
    }
  }
}

function tryParseRequest(
  data: string,
): { entityId: EntityId; componentName: string } | null {
  if (!data) return null;
  const sep = data.indexOf(":");
  if (sep < 0) return null;
  const idStr = data.substring(0, sep);
  const componentName = data.substring(sep + 1);
  if (!idStr || !componentName) return null;
  return { entityId: new EntityId(idStr), componentName };
}
