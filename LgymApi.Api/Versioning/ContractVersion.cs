namespace LgymApi.Api.Versioning;

public readonly record struct ContractVersion(int Major, int Minor, int Patch) : IComparable<ContractVersion>
{
    public static ContractVersion Zero { get; } = new(0, 0, 0);

    public static bool TryParse(string? value, out ContractVersion version)
    {
        version = Zero;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var parts = normalized.Split('.', StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor) ||
            !int.TryParse(parts[2], out var patch) ||
            major < 0 || minor < 0 || patch < 0)
        {
            return false;
        }

        version = new ContractVersion(major, minor, patch);
        return true;
    }

    public static ContractVersion Parse(string value)
    {
        if (!TryParse(value, out var version))
        {
            throw new FormatException($"Invalid contract version '{value}'. Expected semantic version like v1.2.3.");
        }

        return version;
    }

    public int CompareTo(ContractVersion other)
    {
        var majorComparison = Major.CompareTo(other.Major);
        if (majorComparison != 0)
        {
            return majorComparison;
        }

        var minorComparison = Minor.CompareTo(other.Minor);
        if (minorComparison != 0)
        {
            return minorComparison;
        }

        return Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"v{Major}.{Minor}.{Patch}";

    public static bool operator <(ContractVersion left, ContractVersion right) => left.CompareTo(right) < 0;

    public static bool operator <=(ContractVersion left, ContractVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >(ContractVersion left, ContractVersion right) => left.CompareTo(right) > 0;

    public static bool operator >=(ContractVersion left, ContractVersion right) => left.CompareTo(right) >= 0;
}
