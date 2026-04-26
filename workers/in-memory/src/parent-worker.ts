import { parentComponent } from "@ardo314/in-memory";
import { type EntityId, entityIdSchema } from "@engine/core";
import { ComponentWorker, Implements } from "@engine/worker";

@Implements(parentComponent)
export class ParentWorker extends ComponentWorker {
  private _parent: EntityId = entityIdSchema.parse("");

  "core.getParent"() {
    return this._parent;
  }

  "core.setParent"(input: EntityId) {
    this._parent = input;
  }
}
