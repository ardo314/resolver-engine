type Id = string & { readonly __brand: unique symbol };

interface Contract {
  properties: Record<string, any>;
}

interface Component {
  id: Id;
  contract: Contract;
}

export function defineComponent(id: Id, contract: Contract): Component {
  return {
    id: id,
    contract: contract,
  };
}
