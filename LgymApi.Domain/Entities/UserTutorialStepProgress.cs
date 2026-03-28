using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class UserTutorialStepProgress : EntityBase<UserTutorialStepProgress>
{
    public Id<UserTutorialProgress> UserTutorialProgressId { get; set; }
    public TutorialStep TutorialStep { get; set; }
    public DateTimeOffset CompletedAt { get; set; }

    public UserTutorialProgress UserTutorialProgress { get; set; } = null!;
}
