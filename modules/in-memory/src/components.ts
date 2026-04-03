import { defineComponent, entityIdSchema } from "@engine/core";
import { poseSchema } from "@ardo314/core";
import { z } from "zod";

export const nameComponent = defineComponent("in-memory.name", {
  properties: {
    name: z.string(),
  },
});

export const parentComponent = defineComponent("in-memory.parent", {
  properties: {
    parent: entityIdSchema,
  },
});

export const poseComponent = defineComponent("in-memory.pose", {
  properties: {
    pose: poseSchema,
  },
});

export const followPoseComponent = defineComponent("in-memory.follow-pose", {
  properties: {
    target: entityIdSchema,
  },
});
