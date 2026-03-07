using Engine.Core;

namespace Modules.Core.InMemory;

/// <summary>
/// An in-memory implementation of <see cref="IParentResolver"/> that stores the parent entity in a field.
/// </summary>
public class InMemoryParentResolver : IParentResolver
{
    private Entity _parent;

    public InMemoryParentResolver() { }

    public InMemoryParentResolver(Entity parent)
    {
        _parent = parent;
    }

    public Entity Get() => _parent;

    public void Set(Entity value) => _parent = value;
}
