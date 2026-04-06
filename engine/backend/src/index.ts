import { connect } from "nats";
import { EntityHandler } from "./entity-handler";
export { EntityRepository } from "./entity-repository";

const nc = await connect();
const handler = new EntityHandler(nc);

await handler.listen();

console.log("Backend listening on NATS");
