using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class UserTutorialProgress : EntityBase<UserTutorialProgress>
{
    public Id<User> UserId { get; set; }
    public TutorialType TutorialType { get; set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<UserTutorialStepProgress> CompletedSteps { get; set; } = new List<UserTutorialStepProgress>();
}
