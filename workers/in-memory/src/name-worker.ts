import { nameComponent } from "@ardo314/in-memory";
import { ComponentWorker, Implements } from "@engine/worker";

@Implements(nameComponent)
export class NameWorker extends ComponentWorker {
  private _name = "";

  "core.getName"() {
    return this._name;
  }

  "core.setName"(input: string) {
    this._name = input;
  }
}
