import { defineComponent } from "@engine/core";
import {
  getPose,
  setPose,
  getName,
  setName,
  getParent,
  setParent,
} from "@ardo314/core";

export const nameComponent = defineComponent("nova.name", [
  getName,
  setName,
]);

export const parentComponent = defineComponent("nova.parent", [
  getParent,
  setParent,
]);

export const poseComponent = defineComponent("nova.pose", [
  getPose,
  setPose,
]);
