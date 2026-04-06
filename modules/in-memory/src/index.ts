import { defineComponent, entityIdSchema } from "@engine/core";
import {
  poseComponent as corePoseComponent,
  nameComponent as coreNameComponent,
  parentComponent as coreParentComponent,
} from "@ardo314/core";

export const nameComponent = defineComponent("in-memory.name", {
  composites: [coreNameComponent],
});

export const parentComponent = defineComponent("in-memory.parent", {
  composites: [coreParentComponent],
});

export const poseComponent = defineComponent("in-memory.pose", {
  composites: [corePoseComponent],
});

export const followPoseComponent = defineComponent("in-memory.follow-pose", {
  properties: {
    target: entityIdSchema,
  },
});
