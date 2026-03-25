namespace LgymApi.Domain.ValueObjects;

/// <summary>
/// Typed entity identifier that wraps a Guid with compile-time type safety.
/// Ensures Id{User} and Id{Plan} are distinct types at compile time,
/// preventing accidental ID confusion between entity types.
/// </summary>
/// <typeparam name="TEntity">The entity type this ID belongs to.</typeparam>
public readonly record struct Id<TEntity>
{
    /// <summary>
    /// Gets the underlying Guid value.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Creates a new typed ID with the specified Guid value.
    /// </summary>
    /// <param name="value">The Guid value to wrap.</param>
    public Id(Guid value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new typed ID with a new non-empty Guid.
    /// </summary>
    /// <returns>A typed ID with a freshly generated Guid.</returns>
    public static Id<TEntity> New() => new(Guid.NewGuid());

    /// <summary>
    /// Gets a default (empty) typed ID with Guid.Empty.
    /// </summary>
    public static Id<TEntity> Empty => new(Guid.Empty);

    /// <summary>
    /// Checks if this ID is empty (Guid.Empty).
    /// </summary>
    public bool IsEmpty => Value == Guid.Empty;

    /// <summary>
    /// Returns the Guid value as a string.
    /// </summary>
    public override string ToString() => Value.ToString();

    /// <summary>
    /// Explicit unwrapping of the underlying Guid.
    /// </summary>
    public static explicit operator Guid(Id<TEntity> id) => id.Value;

    /// <summary>
    /// Explicit wrapping of a Guid into a typed ID.
    /// </summary>
    public static explicit operator Id<TEntity>(Guid value) => new(value);
}
