namespace LgymApi.BackgroundWorker.Common;

/// <summary>
/// Typed command descriptor that enables durable command type storage and exact-type matching.
/// Uses CLR type FullName as stable discriminator, ensuring exact-type equality only.
/// </summary>
public sealed class CommandDescriptor
{
    /// <summary>
    /// Gets the CLR type FullName of the command (stable form for serialization).
    /// </summary>
    public string TypeFullName { get; }

    /// <summary>
    /// Gets the command type for exact-match resolution.
    /// </summary>
    public Type CommandType { get; }

    /// <summary>
    /// Initializes a new descriptor for the given command type.
    /// </summary>
    /// <param name="commandType">Exact command type to describe.</param>
    /// <exception cref="ArgumentNullException">Thrown when commandType is null.</exception>
    /// <exception cref="ArgumentException">Thrown when command type has no FullName (e.g., open generic).</exception>
    public CommandDescriptor(Type commandType)
    {
        CommandType = commandType;
        TypeFullName = CommandTypeDiscriminatorPolicy.GetDiscriminator(commandType);
    }

    /// <summary>
    /// Private constructor for creating descriptor from persisted type string.
    /// </summary>
    private CommandDescriptor(string typeFullName)
    {
        TypeFullName = typeFullName;
        CommandType = null!;
    }

    /// <summary>
    /// Creates a descriptor from a durable persisted type string (round-trip from storage).
    /// </summary>
    /// <param name="typeFullName">The persisted CLR type FullName.</param>
    /// <returns>A descriptor with the stored type identifier; CommandType is null until resolved.</returns>
    /// <exception cref="ArgumentException">Thrown if typeFullName is null or empty.</exception>
    public static CommandDescriptor FromPersistedType(string typeFullName)
    {
        if (string.IsNullOrWhiteSpace(typeFullName))
            throw new ArgumentException("Type full name must not be null or empty.", nameof(typeFullName));

        return new CommandDescriptor(typeFullName);
    }

    /// <summary>
    /// Checks exact-type equality with another descriptor (no polymorphic matching).
    /// </summary>
    public bool IsExactTypeMatch(CommandDescriptor other)
    {
        if (other == null)
            return false;

        // Exact string equality of type full name; no IsAssignableFrom.
        return CommandTypeDiscriminatorPolicy.IsExactMatch(TypeFullName, other.TypeFullName);
    }

    /// <summary>
    /// Resolves the actual runtime type from a persisted type full name.
    /// Throws if type cannot be found in loaded assemblies.
    /// </summary>
    /// <param name="typeFullName">The CLR type FullName stored durably.</param>
    /// <returns>The resolved Type.</returns>
    /// <exception cref="InvalidOperationException">Thrown if type is not found in any loaded assembly.</exception>
    public static Type ResolveCommandType(string typeFullName)
    {
        return CommandTypeDiscriminatorPolicy.ResolveType(typeFullName);
    }

    public override string ToString() => TypeFullName;

    public override int GetHashCode() => TypeFullName.GetHashCode(StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is CommandDescriptor other && IsExactTypeMatch(other);
}
