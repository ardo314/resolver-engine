/**
 * IParent behaviour contract — data behaviour holding a parent EntityId.
 */
export interface IParent {
  getDataAsync(ct?: AbortSignal): Promise<string>;
  setDataAsync(data: string, ct?: AbortSignal): Promise<void>;
}

export const IParentName = "IParent";
