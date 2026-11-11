using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;

namespace LgymApi.Api.Features.Account.Contracts;

public sealed class LinkGoogleRequest : IDto
{
    [JsonPropertyName("idToken")]
    public string IdToken { get; set; } = string.Empty;

    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }
}

public sealed class ExternalLoginDto : IResultDto
{
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("providerEmail")]
    public string? ProviderEmail { get; set; }
}

public sealed class AccountAccessResponseDto : IResultDto
{
    /// <summary>Contract version. Version 1 is additive and fail-closed.</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }

    [JsonPropertyName("account")]
    public AccountAccessAccountDto Account { get; init; } = new();

    [JsonPropertyName("application")]
    public AccountAccessApplicationCapabilitiesDto Application { get; init; } = new();

    [JsonPropertyName("extensions")]
    public AccountAccessOwnerExtensionsDto Extensions { get; init; } = new();
}

public sealed class AccountAccessAccountDto : IResultDto
{
    [JsonPropertyName("_id")]
    public string Id { get; init; } = string.Empty;
}

public sealed class AccountAccessApplicationCapabilitiesDto : IResultDto
{
    [JsonPropertyName("canAccessAdminWorkspace")]
    public bool CanAccessAdminWorkspace { get; init; }

    [JsonPropertyName("canManageUserRoles")]
    public bool CanManageUserRoles { get; init; }

    [JsonPropertyName("canManageAppConfig")]
    public bool CanManageAppConfig { get; init; }

    [JsonPropertyName("canManageGlobalExercises")]
    public bool CanManageGlobalExercises { get; init; }

    [JsonPropertyName("canAccessTrainerWorkspace")]
    public bool CanAccessTrainerWorkspace { get; init; }
}

public sealed class AccountAccessOwnerExtensionsDto : IResultDto
{
    [JsonPropertyName("reporting")]
    public ReportingAccountAccessOwnerCapabilitiesDto Reporting { get; init; } = new();

    [JsonPropertyName("trainingPlanning")]
    public AccountAccessOwnerCapabilitiesDto TrainingPlanning { get; init; } = new();

    [JsonPropertyName("coaching")]
    public AccountAccessOwnerCapabilitiesDto Coaching { get; init; } = new();

    [JsonPropertyName("nutrition")]
    public NutritionAccountAccessOwnerCapabilitiesDto Nutrition { get; init; } = new();

    [JsonPropertyName("workoutProgress")]
    public AccountAccessOwnerCapabilitiesDto WorkoutProgress { get; init; } = new();

    [JsonPropertyName("notifications")]
    public NotificationsAccountAccessOwnerCapabilitiesDto Notifications { get; init; } = new();
}

public sealed class AccountAccessOwnerCapabilitiesDto : IResultDto
{
    /// <summary>False when the owner is missing, unsupported, or intentionally denies it. Clients must not infer access from availability.</summary>
    [JsonPropertyName("available")]
    public bool IsAvailable { get; init; }
}

public sealed class ReportingAccountAccessOwnerCapabilitiesDto : IResultDto
{
    [JsonPropertyName("available")]
    public bool IsAvailable { get; init; }

    [JsonPropertyName("canReadOwnSubmissions")]
    public bool CanReadOwnSubmissions { get; init; }

    [JsonPropertyName("canSubmitReportRequests")]
    public bool CanSubmitReportRequests { get; init; }

    [JsonPropertyName("canManageReportTemplates")]
    public bool CanManageReportTemplates { get; init; }

    [JsonPropertyName("canManagePhotoTypes")]
    public bool CanManagePhotoTypes { get; init; }
}

public sealed class NotificationsAccountAccessOwnerCapabilitiesDto : IResultDto
{
    [JsonPropertyName("available")]
    public bool IsAvailable { get; init; }

    [JsonPropertyName("canReadNotifications")]
    public bool CanReadNotifications { get; init; }

    [JsonPropertyName("canMarkNotificationsRead")]
    public bool CanMarkNotificationsRead { get; init; }

    [JsonPropertyName("canManagePushInstallations")]
    public bool CanManagePushInstallations { get; init; }
}

public sealed class NutritionAccountAccessOwnerCapabilitiesDto : IResultDto
{
    [JsonPropertyName("available")]
    public bool IsAvailable { get; init; }

    [JsonPropertyName("canReadOwnDietPlans")]
    public bool CanReadOwnDietPlans { get; init; }

    [JsonPropertyName("canUpdateOwnDietPlans")]
    public bool CanUpdateOwnDietPlans { get; init; }

    [JsonPropertyName("canCreateDietPlans")]
    public bool CanCreateDietPlans { get; init; }

    [JsonPropertyName("canManageSupplementation")]
    public bool CanManageSupplementation { get; init; }
}
