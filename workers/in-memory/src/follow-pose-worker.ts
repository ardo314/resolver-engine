import type { Pose } from "@ardo314/core";
import { followPoseComponent } from "@ardo314/in-memory";
import { type EntityId, entityIdSchema } from "@engine/core";
import { ComponentWorker, Implements } from "@engine/worker";

@Implements(followPoseComponent)
export class FollowPoseWorker extends ComponentWorker {
  private _target: EntityId = entityIdSchema.parse("");
  private _pose: Pose = [0, 0, 0, 0, 0, 0];

  "in-memory.getTarget"() {
    return this._target;
  }

  "in-memory.setTarget"(input: EntityId) {
    this._target = input;
  }

  "core.getPose"() {
    return this._pose;
  }

  "core.setPose"(input: Pose) {
    this._pose = input;
  }
}
