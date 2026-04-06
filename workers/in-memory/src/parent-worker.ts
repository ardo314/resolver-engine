import { parentComponent } from "@ardo314/in-memory";
import { type EntityId, entityIdSchema } from "@engine/core";
import {
  ComponentWorker,
  Implements,
  type ComponentProperty,
} from "@engine/worker";

@Implements(parentComponent)
export class ParentWorker extends ComponentWorker {
  private _parent: EntityId = entityIdSchema.parse("");

  parent: ComponentProperty<EntityId> = {
    get: () => this._parent,
    set: (value) => {
      this._parent = value;
    },
  };
}
