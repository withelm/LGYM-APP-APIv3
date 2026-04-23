using Google.Apis.Auth;
using LgymApi.Application.Services;
using Microsoft.Extensions.Configuration;

namespace LgymApi.Infrastructure.Services;

public sealed class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly IConfiguration _configuration;

    public GoogleTokenValidator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<GoogleTokenPayload?> ValidateAsync(string idToken, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var clientId = _configuration["GoogleAuth:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return null;
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [clientId]
                });

            if (string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email))
            {
                return null;
            }

            return new GoogleTokenPayload(
                payload.Subject,
                payload.Email,
                payload.EmailVerified,
                payload.Name,
                payload.Picture);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
