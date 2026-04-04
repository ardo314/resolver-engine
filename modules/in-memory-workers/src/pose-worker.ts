import type { Pose } from "@ardo314/core";
import { poseComponent } from "@ardo314/in-memory";
import { ComponentWorker, Implements } from "@engine/module";

@Implements(poseComponent)
export class PoseWorker extends ComponentWorker {
  pose: Pose = [0, 0, 0, 0, 0, 0];
}
