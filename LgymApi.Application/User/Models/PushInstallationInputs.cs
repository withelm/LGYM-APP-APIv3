namespace LgymApi.Application.Features.User.Models;

public sealed record RegisterPushInstallationInput(
    string InstallationId,
    string Platform,
    string FcmToken,
    string? AppVersion,
    string Environment,
    string? PermissionStatus);

public sealed record PushInstallationActionInput(string InstallationId);
