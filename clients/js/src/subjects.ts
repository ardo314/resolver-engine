export const Subjects = {
  createEntity: "engine.world.createEntity",
  deleteEntity: "engine.world.deleteEntity",
  hasEntity: "engine.world.hasEntity",
  listEntities: "engine.world.listEntities",
  addComponent: "engine.entity.addComponent",
  removeComponent: "engine.entity.removeComponent",
  hasComponent: "engine.entity.hasComponent",
  getComponents: "engine.entity.getComponents",
  queryEntity: "engine.entity.query",
  registerComponent: "engine.component.register",
  listComponents: "engine.component.list",
  startWorker: "engine.worker.start",
  stopWorker: "engine.worker.stop",
} as const;

export const WorkerSubjects = {
  callMethod: (componentId: string, entityId: string, method: string) =>
    `engine.worker.${componentId}.${entityId}.method.${method}`,
} as const;
