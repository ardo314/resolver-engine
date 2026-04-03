import { parentComponent } from "@ardo314/in-memory";
import { type EntityId, entityIdSchema } from "@engine/core";
import { ComponentWorker, Implements, SerializeField } from "@engine/module";

@Implements(parentComponent)
export class ParentWorker extends ComponentWorker {
  @SerializeField(entityIdSchema)
  parent: EntityId = entityIdSchema.parse("");
}
