using Engine.Core;

namespace Modules.Spatial;

/// <summary>
/// Abstract component representing a rigid body transform in 3D space.
/// </summary>
public abstract class RigidTransform3D : IComponent<RigidTransform3DData>
{
    /// <inheritdoc />
    public abstract RigidTransform3DData Get();

    /// <inheritdoc />
    public abstract void Set(RigidTransform3DData value);
}
