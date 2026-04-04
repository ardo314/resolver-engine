import { nameComponent } from "@ardo314/in-memory";
import { ComponentWorker, Implements } from "@engine/module";

@Implements(nameComponent)
export class NameWorker extends ComponentWorker {
  name = "";
}
