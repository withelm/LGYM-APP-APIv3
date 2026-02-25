using LgymApi.Domain.Enums;

namespace LgymApi.Domain.Entities;

public sealed class TrainerInvitation : EntityBase
{
    public Guid TrainerId { get; set; }
    public Guid TraineeId { get; set; }
    public string Code { get; set; } = string.Empty;
    public TrainerInvitationStatus Status { get; set; } = TrainerInvitationStatus.Pending;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }

    public User Trainer { get; set; } = null!;
    public User Trainee { get; set; } = null!;
}
