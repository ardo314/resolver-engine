type Id = string & { readonly __brand: unique symbol };

interface ComponentContract {
  properties: Record<string, any>;
}

interface Component {
  id: Id;
  contract: ComponentContract;
}

export function defineComponent(
  id: Id,
  contract: ComponentContract,
): Component {
  return {
    id: id,
    contract: contract,
  };
}
