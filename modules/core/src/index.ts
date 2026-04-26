import { z } from "zod";
import { defineComponent, defineMethod, entityIdSchema } from "@engine/core";

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

// --- Standalone method definitions ---

export const getPose = defineMethod("core.getPose", {
  output: poseSchema,
});

export const setPose = defineMethod("core.setPose", {
  input: poseSchema,
});

export const getName = defineMethod("core.getName", {
  output: z.string(),
});

export const setName = defineMethod("core.setName", {
  input: z.string(),
});

export const getParent = defineMethod("core.getParent", {
  output: entityIdSchema,
});

export const setParent = defineMethod("core.setParent", {
  input: entityIdSchema,
});

// --- Core components ---

export const poseComponent = defineComponent("core.pose", [getPose, setPose]);

export const nameComponent = defineComponent("core.name", [getName, setName]);

export const parentComponent = defineComponent("core.parent", [
  getParent,
  setParent,
]);
