import { type Pose, poseSchema } from "@ardo314/core";
import { poseComponent } from "@ardo314/in-memory";
import { ComponentWorker, Implements, SerializeField } from "@engine/module";

@Implements(poseComponent)
export class PoseWorker extends ComponentWorker {
  @SerializeField(poseSchema)
  pose: Pose = [0, 0, 0, 0, 0, 0];
}
