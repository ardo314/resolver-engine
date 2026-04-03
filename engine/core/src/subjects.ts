export const Subjects = {
  createEntity: "engine.world.createEntity",
  deleteEntity: "engine.world.deleteEntity",
  hasEntity: "engine.world.hasEntity",
  listEntities: "engine.world.listEntities",
  addComponent: "engine.entity.addComponent",
  removeComponent: "engine.entity.removeComponent",
  hasComponent: "engine.entity.hasComponent",
  getComponents: "engine.entity.getComponents",
} as const;

export const WorkerSubjects = {
  getProperty: (componentId: string, entityId: string) =>
    `engine.worker.${componentId}.${entityId}.getProperty`,
  setProperty: (componentId: string, entityId: string) =>
    `engine.worker.${componentId}.${entityId}.setProperty`,
} as const;
