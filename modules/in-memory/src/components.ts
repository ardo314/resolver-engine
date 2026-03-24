/**
 * Component marker for InMemoryPose.
 * Declares that it provides the IPose behaviour.
 */
export const InMemoryPose = {
  componentName: "InMemoryPose",
  behaviourNames: ["IPose"],
} as const;

/**
 * Component marker for InMemoryParent.
 * Declares that it provides the IParent behaviour.
 */
export const InMemoryParent = {
  componentName: "InMemoryParent",
  behaviourNames: ["IParent"],
} as const;
