using System.Numerics;

namespace Modules.Spatial;

/// <summary>
/// Represents a rigid body transform in 3D space (position + rotation).
/// </summary>
public readonly struct RigidTransform3DData : IEquatable<RigidTransform3DData>
{
    public Vector3 Position { get; }
    public Quaternion Rotation { get; }

    public RigidTransform3DData(Vector3 position, Quaternion rotation)
    {
        Position = position;
        Rotation = rotation;
    }

    public static RigidTransform3DData Identity => new(Vector3.Zero, Quaternion.Identity);

    public bool Equals(RigidTransform3DData other) =>
        Position.Equals(other.Position) && Rotation.Equals(other.Rotation);

    public override bool Equals(object? obj) => obj is RigidTransform3DData other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Position, Rotation);

    public override string ToString() =>
        $"RigidTransform3DData(Position: {Position}, Rotation: {Rotation})";

    public static bool operator ==(RigidTransform3DData left, RigidTransform3DData right) =>
        left.Equals(right);

    public static bool operator !=(RigidTransform3DData left, RigidTransform3DData right) =>
        !left.Equals(right);
}
