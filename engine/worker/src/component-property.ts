export type ComponentProperty<T> = {
  get(): T | Promise<T>;
  set(value: T): void | Promise<void>;
};
