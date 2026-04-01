import { connect } from "nats";
import { EntityHandler } from "./entity-handler.js";
import {
  NameWorker,
  ParentWorker,
  PoseWorker,
  FollowPoseWorker,
} from "@ardo314/in-memory-workers";
export { EntityRepository } from "./entity-repository.js";

const nc = await connect();
const handler = new EntityHandler(nc);

handler.registerWorker(NameWorker);
handler.registerWorker(ParentWorker);
handler.registerWorker(PoseWorker);
handler.registerWorker(FollowPoseWorker);

await handler.listen();

console.log("Backend listening on NATS");
