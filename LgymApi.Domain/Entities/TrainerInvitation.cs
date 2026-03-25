using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class TrainerInvitation : EntityBase<TrainerInvitation>
{
    public Id<User> TrainerId { get; set; }
    public Id<User> TraineeId { get; set; }
    public string Code { get; set; } = string.Empty;
    public TrainerInvitationStatus Status { get; set; } = TrainerInvitationStatus.Pending;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }

    public User Trainer { get; set; } = null!;
    public User Trainee { get; set; } = null!;
}
