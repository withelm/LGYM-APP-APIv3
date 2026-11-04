using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class TrainerApplicationAttempt : EntityBase<TrainerApplicationAttempt>
{
    public Id<User> ApplicantId { get; set; }
    public User Applicant { get; set; } = null!;
    public int Attempt { get; set; }
    public TrainerApprovalStatus Status { get; set; }
    public Id<User>? ReviewedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }

    public static TrainerApplicationAttempt Submit(Id<User> applicantId, int attempt) => new()
    {
        Id = Id<TrainerApplicationAttempt>.New(),
        ApplicantId = applicantId,
        Attempt = attempt,
        Status = TrainerApprovalStatus.PendingApproval
    };
}
