import { nameSchema } from "@ardo314/in-memory";
import { ComponentWorker, Implements, SerializeField } from "@engine/module";
import { z } from "zod";

@Implements(nameSchema)
export class NameWorker extends ComponentWorker {
  @SerializeField(z.string())
  value = "";
}
