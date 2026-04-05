using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.TrainerRelationships.Models;

public sealed class TrainerInvitationResult
{
    public LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation> Id { get; init; }
    public LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User> TrainerId { get; init; }
    public LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>? TraineeId { get; init; }
    public string Code { get; init; } = string.Empty;
    public TrainerInvitationStatus Status { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? RespondedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
