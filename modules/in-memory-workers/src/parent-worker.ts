import { parentComponent } from "@ardo314/in-memory";
import { type EntityId, entityIdSchema } from "@engine/core";
import { ComponentWorker, Implements } from "@engine/module";

@Implements(parentComponent)
export class ParentWorker extends ComponentWorker {
  parent: EntityId = entityIdSchema.parse("");
}
