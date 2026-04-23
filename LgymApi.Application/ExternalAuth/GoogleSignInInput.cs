namespace LgymApi.Application.ExternalAuth;

public sealed class GoogleSignInInput
{
    public string IdToken { get; init; } = string.Empty;
}
