import { connect } from "nats";
import { EntityHandler } from "./entity-handler";
export { EntityRepository } from "./entity-repository";

const nc = await connect({ servers: process.env.NATS_URL });
const handler = new EntityHandler(nc);

await handler.listen();

console.log("Backend listening on NATS");
