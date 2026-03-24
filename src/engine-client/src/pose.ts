/**
 * Pose data: position + rotation.
 */
export interface Pose {
  position: { x: number; y: number; z: number };
  rotation: { x: number; y: number; z: number; w: number };
}

/**
 * IPose behaviour contract — data behaviour holding Pose data.
 */
export interface IPose {
  getDataAsync(ct?: AbortSignal): Promise<Pose>;
  setDataAsync(data: Pose, ct?: AbortSignal): Promise<void>;
}

export const IPoseName = "IPose";
