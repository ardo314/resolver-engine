import { connect } from "nats";
import { EntityHandler } from "./entity-handler.js";
export { EntityRepository } from "./entity-repository.js";

const nc = await connect({
  servers: process.env.NATS_URL,
  user: process.env.NATS_USER,
  pass: process.env.NATS_PASS,
});
const handler = new EntityHandler(nc);

await handler.listen();

console.log("Backend listening on NATS");

const shutdown = async () => {
  console.log("Draining NATS connection...");
  await nc.drain();
};
process.on("SIGTERM", shutdown);
process.on("SIGINT", shutdown);

await nc.closed();
