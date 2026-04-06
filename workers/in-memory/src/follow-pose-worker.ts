import { followPoseComponent } from "@ardo314/in-memory";
import { type EntityId, entityIdSchema } from "@engine/core";
import {
  ComponentWorker,
  Implements,
  type ComponentProperty,
} from "@engine/worker";

@Implements(followPoseComponent)
export class FollowPoseWorker extends ComponentWorker {
  private _target: EntityId = entityIdSchema.parse("");

  target: ComponentProperty<EntityId> = {
    get: () => this._target,
    set: (value) => {
      this._target = value;
    },
  };
}
