namespace LgymApi.Application.Services;

public sealed record GoogleTokenPayload(string Subject, string Email, bool EmailVerified, string? Name, string? Picture);
