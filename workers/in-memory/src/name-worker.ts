import { nameComponent } from "@ardo314/in-memory";
import {
  ComponentWorker,
  Implements,
  type ComponentProperty,
} from "@engine/worker";

@Implements(nameComponent)
export class NameWorker extends ComponentWorker {
  private _name = "";

  name: ComponentProperty<string> = {
    get: () => this._name,
    set: (value) => {
      this._name = value;
    },
  };
}
