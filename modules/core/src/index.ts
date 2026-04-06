import { z } from "zod";
import { defineComponent, entityIdSchema } from "@engine/core";

export const vector2Schema = z.tuple([z.number(), z.number()]);

export type Vector2 = z.infer<typeof vector2Schema>;

export const vector3Schema = z.tuple([z.number(), z.number(), z.number()]);

export type Vector3 = z.infer<typeof vector3Schema>;

export const rotationVectorSchema = z.tuple([
  z.number(),
  z.number(),
  z.number(),
]);

export type RotationVector = z.infer<typeof rotationVectorSchema>;

export const quaternionSchema = z.tuple([
  z.number(),
  z.number(),
  z.number(),
  z.number(),
]);

export type Quaternion = z.infer<typeof quaternionSchema>;

export const poseSchema = z.tuple([
  z.number(),
  z.number(),
  z.number(),
  z.number(),
  z.number(),
  z.number(),
]);

export type Pose = z.infer<typeof poseSchema>;

export namespace Pose {
  export function position(pose: Pose): Vector3 {
    return [pose[0], pose[1], pose[2]];
  }

  export function rotation(pose: Pose): RotationVector {
    return [pose[3], pose[4], pose[5]];
  }

  export function rotationVector(pose: Pose): RotationVector {
    return [pose[3], pose[4], pose[5]];
  }
}

// --- Core components ---

export const poseComponent = defineComponent("core.pose", {
  properties: {
    pose: poseSchema,
  },
});

export const nameComponent = defineComponent("core.name", {
  properties: {
    name: z.string(),
  },
});

export const parentComponent = defineComponent("core.parent", {
  properties: {
    parent: entityIdSchema,
  },
});
