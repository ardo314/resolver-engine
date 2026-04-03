import { nameComponent } from "@ardo314/in-memory";
import { ComponentWorker, Implements, SerializeField } from "@engine/module";
import { z } from "zod";

@Implements(nameComponent)
export class NameWorker extends ComponentWorker {
  @SerializeField(z.string())
  name = "";
}
