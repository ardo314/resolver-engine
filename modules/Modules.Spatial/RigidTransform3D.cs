using System.Numerics;

namespace Modules.Spatial;

/// <summary>
/// Represents a rigid body transform in 3D space (position + rotation).
/// </summary>
public readonly struct RigidTransform3D : IEquatable<RigidTransform3D>
{
    public Vector3 Position { get; }
    public Quaternion Rotation { get; }

    public RigidTransform3D(Vector3 position, Quaternion rotation)
    {
        Position = position;
        Rotation = rotation;
    }

    public static RigidTransform3D Identity => new(Vector3.Zero, Quaternion.Identity);

    public bool Equals(RigidTransform3D other) =>
        Position.Equals(other.Position) && Rotation.Equals(other.Rotation);

    public override bool Equals(object? obj) => obj is RigidTransform3D other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Position, Rotation);

    public override string ToString() =>
        $"RigidTransform3D(Position: {Position}, Rotation: {Rotation})";

    public static bool operator ==(RigidTransform3D left, RigidTransform3D right) =>
        left.Equals(right);

    public static bool operator !=(RigidTransform3D left, RigidTransform3D right) =>
        !left.Equals(right);
}
