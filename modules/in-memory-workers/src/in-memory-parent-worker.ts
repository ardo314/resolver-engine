import { ComponentWorker } from "engine-worker";
import { encode, decode } from "@msgpack/msgpack";

/**
 * In-memory worker for the IParent behaviour.
 * Stores a parent EntityId per entity.
 */
export class InMemoryParentWorker extends ComponentWorker {
  readonly componentName = "InMemoryParent";
  readonly behaviourNames = ["IParent"];

  private parentId: string | null = null;

  async dispatch(
    _behaviourName: string,
    methodName: string,
    payload: Uint8Array,
  ): Promise<Uint8Array> {
    switch (methodName) {
      case "GetDataAsync": {
        if (this.parentId === null) {
          throw new Error("Parent ID has not been set.");
        }
        return encode(this.parentId);
      }
      case "SetDataAsync": {
        this.parentId = decode(payload) as string;
        return new Uint8Array();
      }
      default:
        throw new Error(`Unknown method '${methodName}' on behaviour IParent`);
    }
  }
}
