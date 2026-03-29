import { parent } from "@ardo314/in-memory";
import { EntityId, entityIdSchema } from "@engine/core";
import { defineComponentWorker } from "@engine/module";

export const parentWorker = defineComponentWorker(parent, () => {
  let _value = entityIdSchema.parse("");

  const value = {
    async get() {
      return _value;
    },
    async set(v: EntityId) {
      _value = entityIdSchema.parse(v);
    },
  };

  return {
    value,
  };
});
