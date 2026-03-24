/**
 * Marker interface for behaviour contracts.
 * Behaviours define data and logic interfaces (e.g. IPose, IParent) that
 * component classes declare support for via the hasBehaviours metadata.
 */
export interface IBehaviour {
  readonly behaviourName: string;
}

/**
 * Convenience base for behaviours that hold typed data with async get/set methods.
 */
export interface IDataBehaviour<T> extends IBehaviour {
  getDataAsync(ct?: AbortSignal): Promise<T>;
  setDataAsync(data: T, ct?: AbortSignal): Promise<void>;
}

/**
 * Marker interface for all generated/registered client-side proxies.
 */
export interface IProxy {
  readonly proxyName: string;
}
