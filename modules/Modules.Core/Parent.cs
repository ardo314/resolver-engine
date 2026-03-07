using Engine.Core;

namespace Modules.Core;

/// <summary>
/// Abstract component representing a parent-child entity relationship.
/// </summary>
public abstract class Parent : IComponent<Entity>
{
    /// <inheritdoc />
    public abstract Entity Get();

    /// <inheritdoc />
    public abstract void Set(Entity value);
}
