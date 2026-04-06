export const Subjects = {
  createEntity: "engine.world.createEntity",
  deleteEntity: "engine.world.deleteEntity",
  hasEntity: "engine.world.hasEntity",
  listEntities: "engine.world.listEntities",
  addComponent: "engine.entity.addComponent",
  removeComponent: "engine.entity.removeComponent",
  hasComponent: "engine.entity.hasComponent",
  getComponents: "engine.entity.getComponents",
  registerComponent: "engine.component.register",
  startWorker: "engine.worker.start",
  stopWorker: "engine.worker.stop",
} as const;

export const WorkerSubjects = {
  getProperty: (componentId: string, entityId: string, property: string) =>
    `engine.worker.${componentId}.${entityId}.property.${property}.get`,
  setProperty: (componentId: string, entityId: string, property: string) =>
    `engine.worker.${componentId}.${entityId}.property.${property}.set`,
  callMethod: (componentId: string, entityId: string, method: string) =>
    `engine.worker.${componentId}.${entityId}.method.${method}`,
} as const;
