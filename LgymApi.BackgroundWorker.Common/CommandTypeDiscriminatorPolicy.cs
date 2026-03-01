namespace LgymApi.BackgroundWorker.Common;

/// <summary>
/// Internal command-type discriminator policy for durable storage and exact-type matching.
/// This policy is not part of public dispatch API and does not expose string routing semantics.
/// </summary>
public static class CommandTypeDiscriminatorPolicy
{
    public static string GetDiscriminator(Type commandType)
    {
        ArgumentNullException.ThrowIfNull(commandType);

        if (string.IsNullOrWhiteSpace(commandType.FullName))
        {
            throw new ArgumentException(
                $"Command type '{commandType}' must have a stable FullName discriminator.",
                nameof(commandType));
        }

        return commandType.FullName;
    }

    public static Type ResolveType(string discriminator)
    {
        if (string.IsNullOrWhiteSpace(discriminator))
        {
            throw new ArgumentException("Command type discriminator must not be null or empty.", nameof(discriminator));
        }

        var resolvedType = Type.GetType(discriminator, throwOnError: false);
        if (resolvedType != null)
        {
            return resolvedType;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            resolvedType = assembly.GetType(discriminator, throwOnError: false);
            if (resolvedType != null)
            {
                return resolvedType;
            }
        }

        throw new InvalidOperationException(
            $"Cannot resolve command type discriminator '{discriminator}' from loaded assemblies.");
    }

    public static bool IsExactMatch(string leftDiscriminator, string rightDiscriminator)
    {
        if (string.IsNullOrWhiteSpace(leftDiscriminator) || string.IsNullOrWhiteSpace(rightDiscriminator))
        {
            return false;
        }

        return string.Equals(leftDiscriminator, rightDiscriminator, StringComparison.Ordinal);
    }
}
