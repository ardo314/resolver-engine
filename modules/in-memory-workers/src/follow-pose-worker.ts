import { followPoseComponent } from "@ardo314/in-memory";
import { type EntityId, entityIdSchema } from "@engine/core";
import { ComponentWorker, Implements, SerializeField } from "@engine/module";

@Implements(followPoseComponent)
export class FollowPoseWorker extends ComponentWorker {
  @SerializeField(entityIdSchema)
  target: EntityId = entityIdSchema.parse("");
}
