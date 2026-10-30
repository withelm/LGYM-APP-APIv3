namespace LgymApi.Api.Middleware;

public enum ReportingProtocolRequestKind
{
    None,
    Template,
    Upload
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ReportingProtocolAttribute : Attribute
{
    public ReportingProtocolAttribute(int minimumVersion = 1)
    {
        MinimumVersion = minimumVersion;
    }

    public int MinimumVersion { get; }
    public ReportingProtocolRequestKind RequestKind { get; init; }
}

internal sealed record ReportingProtocolMetadata(int MinimumVersion, ReportingProtocolRequestKind RequestKind)
{
    public static ReportingProtocolMetadata? Resolve(Endpoint? endpoint)
    {
        var declarations = endpoint?.Metadata.GetOrderedMetadata<ReportingProtocolAttribute>();
        if (declarations is null || declarations.Count == 0)
        {
            return null;
        }

        var requestKinds = declarations
            .Select(declaration => declaration.RequestKind)
            .Where(kind => kind != ReportingProtocolRequestKind.None)
            .Distinct()
            .ToArray();
        if (requestKinds.Length > 1)
        {
            throw new InvalidOperationException("A reporting endpoint declares conflicting protocol request kinds.");
        }

        return new ReportingProtocolMetadata(
            declarations.Max(declaration => declaration.MinimumVersion),
            requestKinds.SingleOrDefault());
    }
}

internal static class ReportingProtocolContext
{
    private static readonly object VersionKey = new();

    public static void SetVersion(HttpContext context, int version) => context.Items[VersionKey] = version;

    public static bool TryGetVersion(HttpContext context, out int version)
    {
        if (context.Items.TryGetValue(VersionKey, out var value) && value is int parsed)
        {
            version = parsed;
            return true;
        }

        version = 0;
        return false;
    }

    public static int GetVersion(HttpContext context) =>
        TryGetVersion(context, out var version)
            ? version
            : throw new InvalidOperationException("The reporting protocol has not been negotiated.");
}
