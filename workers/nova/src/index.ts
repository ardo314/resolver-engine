import { connect } from "nats";
import { WorkerHost } from "@engine/worker";
import { NameWorker } from "./name-worker.js";
import { ParentWorker } from "./parent-worker.js";
import { PoseWorker } from "./pose-worker.js";

const nc = await connect();
const host = new WorkerHost(nc);

host.registerWorker(NameWorker);
host.registerWorker(ParentWorker);
host.registerWorker(PoseWorker);

await host.listen();

console.log("Worker host listening on NATS");
