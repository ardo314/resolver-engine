import { ComponentWorker } from "engine-worker";
import { encode, decode } from "@msgpack/msgpack";
import type { Pose } from "engine-client";

/**
 * In-memory worker for the IPose behaviour.
 * Stores position and rotation per entity.
 */
export class InMemoryPoseWorker extends ComponentWorker {
  readonly componentName = "InMemoryPose";
  readonly behaviourNames = ["IPose"];

  private pose: Pose = {
    position: { x: 0, y: 0, z: 0 },
    rotation: { x: 0, y: 0, z: 0, w: 1 },
  };

  async dispatch(
    _behaviourName: string,
    methodName: string,
    payload: Uint8Array,
  ): Promise<Uint8Array> {
    switch (methodName) {
      case "GetDataAsync": {
        return encode(this.pose);
      }
      case "SetDataAsync": {
        this.pose = decode(payload) as Pose;
        return new Uint8Array();
      }
      default:
        throw new Error(`Unknown method '${methodName}' on behaviour IPose`);
    }
  }
}
