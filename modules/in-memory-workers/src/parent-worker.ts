import { parentSchema } from "@ardo314/in-memory";
import { type EntityId, entityIdSchema } from "@engine/core";
import { ComponentWorker, Implements, SerializeField } from "@engine/module";

@Implements(parentSchema)
export class ParentWorker extends ComponentWorker {
  @SerializeField(entityIdSchema)
  value: EntityId = entityIdSchema.parse("");
}
