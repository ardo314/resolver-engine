import type { Pose } from "@ardo314/core";
import { poseComponent } from "@ardo314/in-memory";
import { ComponentWorker, Implements } from "@engine/worker";

@Implements(poseComponent)
export class PoseWorker extends ComponentWorker {
  private _pose: Pose = [0, 0, 0, 0, 0, 0];

  "core.getPose"() {
    return this._pose;
  }

  "core.setPose"(input: Pose) {
    this._pose = input;
  }
}
