using Engine.Core;
using Modules.Core;

namespace Modules.Core.InMemory;

/// <summary>
/// An in-memory implementation of <see cref="Parent"/> that stores the parent entity in a field.
/// </summary>
public class InMemoryParent : Parent
{
    private Entity _parent;

    public InMemoryParent() { }

    public InMemoryParent(Entity parent)
    {
        _parent = parent;
    }

    public override Entity Get() => _parent;

    public override void Set(Entity value) => _parent = value;
}
