namespace LgymApi.Api.Constants;

using LgymApi.Domain.Security;

public static class ConfigKeys
{
    public static readonly string JwtSigningKey = AuthConstants.ConfigKeys.JwtSigningKey;
    public const string CorsAllowedOrigins = "Cors:AllowedOrigins";
}
