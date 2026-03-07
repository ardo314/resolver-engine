using System;

namespace Engine.Core;

/// <summary>
/// Represents a lightweight entity identifier.
/// </summary>
public readonly struct Entity : IEquatable<Entity>
{
    public Guid Id { get; }

    public Entity(Guid id)
    {
        Id = id;
    }

    public static Entity New() => new Entity(Guid.NewGuid());

    public bool Equals(Entity other) => Id.Equals(other.Id);

    public override bool Equals(object? obj) => obj is Entity other && Equals(other);

    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() => Id.ToString();

    public static bool operator ==(Entity left, Entity right) => left.Equals(right);

    public static bool operator !=(Entity left, Entity right) => !left.Equals(right);
}
