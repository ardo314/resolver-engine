import { connect } from "nats";
import { WorkerHost } from "@engine/module";
import { NameWorker } from "./name-worker.js";
import { ParentWorker } from "./parent-worker.js";
import { PoseWorker } from "./pose-worker.js";
import { FollowPoseWorker } from "./follow-pose-worker.js";
import { TransformWorker } from "./transform-worker.js";

export {
  NameWorker,
  ParentWorker,
  PoseWorker,
  FollowPoseWorker,
  TransformWorker,
};

const nc = await connect();
const host = new WorkerHost(nc);

host.registerWorker(NameWorker);
host.registerWorker(ParentWorker);
host.registerWorker(PoseWorker);
host.registerWorker(FollowPoseWorker);
host.registerWorker(TransformWorker);

await host.listen();

console.log("Worker host listening on NATS");
