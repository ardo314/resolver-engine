import { startWorkerRuntime } from "engine-worker-runtime";
import type { WorkerRegistration } from "engine-worker";
import { InMemoryPoseWorker } from "./in-memory-pose-worker.js";
import { InMemoryParentWorker } from "./in-memory-parent-worker.js";

const registrations: WorkerRegistration[] = [
  {
    componentName: "InMemoryPose",
    behaviourNames: ["IPose"],
    create: () => new InMemoryPoseWorker(),
  },
  {
    componentName: "InMemoryParent",
    behaviourNames: ["IParent"],
    create: () => new InMemoryParentWorker(),
  },
];

startWorkerRuntime(registrations).catch((err) => {
  console.error("Fatal error:", err);
  process.exit(1);
});
