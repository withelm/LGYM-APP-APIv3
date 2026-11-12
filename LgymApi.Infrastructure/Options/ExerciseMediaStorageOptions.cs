namespace LgymApi.Infrastructure.Options;

public sealed class ExerciseMediaStorageOptions
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
    public string PublicBaseUrl { get; set; } = string.Empty;
    public bool AllowDevelopmentR2Dev { get; set; }

    public void Validate(string environmentName = "Production")
    {
        if (!Enabled) return;
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https" || !endpoint.Host.EndsWith(".r2.cloudflarestorage.com", StringComparison.Ordinal) || endpoint.AbsolutePath != "/" || endpoint.UserInfo.Length > 0 || endpoint.Query.Length > 0 || endpoint.Fragment.Length > 0 || !endpoint.IsDefaultPort) throw new InvalidOperationException("ExerciseMediaStorage requires an HTTPS R2 endpoint.");
        if (string.IsNullOrWhiteSpace(Bucket) || Bucket.Length is < 3 or > 63 || Bucket[0] == '-' || Bucket[^1] == '-'
            || Bucket.Any(character => character is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '-')))
            throw new InvalidOperationException("ExerciseMediaStorage requires a valid R2 bucket name.");
        if (!Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var cdn) || cdn.Scheme != "https" || cdn.AbsolutePath != "/" || cdn.Query.Length > 0 || cdn.Fragment.Length > 0 || cdn.UserInfo.Length > 0 || !cdn.IsDefaultPort)
            throw new InvalidOperationException("ExerciseMediaStorage requires a dedicated HTTPS public bucket origin.");
        var host = cdn.IdnHost.TrimEnd('.');
        if (host == "r2.cloudflarestorage.com" || host.EndsWith(".r2.cloudflarestorage.com", StringComparison.Ordinal)
            || host == "r2.dev" || (host.EndsWith(".r2.dev", StringComparison.Ordinal)
                && !(AllowDevelopmentR2Dev && string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))))
            throw new InvalidOperationException("ExerciseMediaStorage requires a custom domain, or explicitly enabled r2.dev in Development only.");
        if (string.IsNullOrWhiteSpace(AccessKeyId) || string.IsNullOrWhiteSpace(SecretAccessKey)) throw new InvalidOperationException("ExerciseMediaStorage requires dedicated credentials.");
    }
}
