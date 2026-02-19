namespace LgymApi.Application.Features.TrainerRelationships.Models;

public sealed class TrainerDashboardTraineeResult
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Avatar { get; init; }
    public TrainerDashboardTraineeStatus Status { get; init; }
    public bool IsLinked { get; init; }
    public bool HasPendingInvitation { get; init; }
    public bool HasExpiredInvitation { get; init; }
    public DateTimeOffset? LinkedAt { get; init; }
    public DateTimeOffset? LastInvitationExpiresAt { get; init; }
    public DateTimeOffset? LastInvitationRespondedAt { get; init; }
}
