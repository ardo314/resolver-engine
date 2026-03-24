import { EntityId } from "engine-core";

/**
 * Abstract base class for module workers. Each concrete worker handles one
 * component type and is instantiated per (EntityId, componentName) pair.
 */
export abstract class ComponentWorker {
  /**
   * The name of the component this worker handles (e.g. "InMemoryPose").
   */
  abstract readonly componentName: string;

  /**
   * The behaviour interface names this worker provides (e.g. ["IPose"]).
   */
  abstract readonly behaviourNames: string[];

  /**
   * The entity this worker belongs to. Set by the runtime after construction.
   */
  entityId!: EntityId;

  /**
   * Called when the component is attached to an entity.
   */
  async onAdded(): Promise<void> {}

  /**
   * Called when the component is removed from an entity.
   */
  async onRemoved(): Promise<void> {}

  /**
   * Dispatches a behaviour method call.
   * @param behaviourName The name of the behaviour interface (e.g. "IPose").
   * @param methodName The method name to invoke.
   * @param payload MessagePack-serialized parameter data (empty if no parameter).
   * @returns MessagePack-serialized return value, or empty if void return.
   */
  abstract dispatch(
    behaviourName: string,
    methodName: string,
    payload: Uint8Array,
  ): Promise<Uint8Array>;
}

/**
 * A factory function that creates ComponentWorker instances.
 */
export interface WorkerRegistration {
  /** The component struct/class name (e.g. "InMemoryPose"). */
  componentName: string;
  /** The behaviour interfaces this component provides (e.g. ["IPose"]). */
  behaviourNames: string[];
  /** Factory function to create a new worker instance. */
  create: () => ComponentWorker;
}
