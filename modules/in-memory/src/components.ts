import { defineComponent } from "@engine/core";
import { entityIdSchema } from "@engine/core/src/entity-id.js";
import { z } from "zod";

export const name = defineComponent("in-memory.name", {
  properties: {
    value: z.string(),
  },
});

export const parent = defineComponent("in-memory.parent", {
  properties: {
    value: entityIdSchema,
  },
});

export const pose = defineComponent("in-memory.pose", {
  properties: {
    position: z.tuple([z.number(), z.number(), z.number()]),
    rotation: z.tuple([z.number(), z.number(), z.number()]),
  },
});
