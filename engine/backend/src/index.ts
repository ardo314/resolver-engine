import { connect } from "nats";
import { EntityHandler } from "./entity-handler.js";
export { EntityRepository } from "./entity-repository.js";

const nc = await connect();
const handler = new EntityHandler(nc);
await handler.listen();

console.log("Backend listening on NATS");
