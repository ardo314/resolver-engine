import type { Component, ComponentReference } from "@engine/core";

export type ComponentWorker<C extends Component = Component> = {
  readonly component: C;
  create(): ComponentReference<C>;
};

export function defineComponentWorker<C extends Component>(
  component: C,
  create: () => ComponentReference<C>,
): ComponentWorker<C> {
  return { component, create };
}
