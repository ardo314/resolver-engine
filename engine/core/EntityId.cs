namespace Engine.Core;

/// <summary>
/// Strongly-typed wrapper for entity identifiers.
/// Format: "{timestamp}-{sequence}", e.g. "1716950400000-0".
/// </summary>
public readonly record struct EntityId(string Value)
{
    public override string ToString() => Value;

    public static implicit operator string(EntityId id) => id.Value;
    public static explicit operator EntityId(string value) => new(value);
}
