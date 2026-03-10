using LgymApi.Domain.Enums;

namespace LgymApi.Domain.Entities;

public sealed class UserTutorialProgress : EntityBase
{
    public Guid UserId { get; set; }
    public TutorialType TutorialType { get; set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<UserTutorialStepProgress> CompletedSteps { get; set; } = new List<UserTutorialStepProgress>();
}
