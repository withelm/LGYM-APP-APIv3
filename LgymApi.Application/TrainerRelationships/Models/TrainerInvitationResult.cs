using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.TrainerRelationships.Models;

public sealed class TrainerInvitationResult
{
    public Guid Id { get; init; }
    public Guid TrainerId { get; init; }
    public Guid TraineeId { get; init; }
    public string Code { get; init; } = string.Empty;
    public TrainerInvitationStatus Status { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? RespondedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
