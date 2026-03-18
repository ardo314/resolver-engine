using Engine.Client;

namespace Modules.InMemoryParent.Core;

[Has<IParent>]
public struct InMemoryParent : IComponent { }
