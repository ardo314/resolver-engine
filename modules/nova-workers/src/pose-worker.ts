import type { Pose } from "@ardo314/core";
import { poseComponent } from "@ardo314/nova";
import {
  ComponentWorker,
  Implements,
  type ComponentProperty,
} from "@engine/module";

@Implements(poseComponent)
export class PoseWorker extends ComponentWorker {
  private _pose: Pose = [0, 0, 0, 0, 0, 0];

  pose: ComponentProperty<Pose> = {
    get: () => this._pose,
    set: (value) => {
      this._pose = value;
    },
  };
}
