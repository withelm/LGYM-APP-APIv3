using LgymApi.Domain.Enums;

namespace LgymApi.Domain.Entities;

public sealed class UserTutorialStepProgress : EntityBase
{
    public Guid UserTutorialProgressId { get; set; }
    public TutorialStep TutorialStep { get; set; }
    public DateTimeOffset CompletedAt { get; set; }

    public UserTutorialProgress UserTutorialProgress { get; set; } = null!;
}
