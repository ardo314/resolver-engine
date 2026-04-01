import { type Pose, poseSchema } from "@ardo314/core";
import { posePropertySchema } from "@ardo314/in-memory";
import { ComponentWorker, Implements, SerializeField } from "@engine/module";

@Implements(posePropertySchema)
export class TransformWorker extends ComponentWorker {
  @SerializeField(poseSchema)
  value: Pose = [0, 0, 0, 0, 0, 0];
}
