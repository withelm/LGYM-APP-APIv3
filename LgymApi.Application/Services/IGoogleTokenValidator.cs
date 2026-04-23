namespace LgymApi.Application.Services;

public interface IGoogleTokenValidator
{
    Task<GoogleTokenPayload?> ValidateAsync(string idToken, CancellationToken ct);
}
