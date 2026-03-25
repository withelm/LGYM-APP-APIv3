using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class TrainerTraineeLink : EntityBase<TrainerTraineeLink>
{
    public Id<User> TrainerId { get; set; }
    public Id<User> TraineeId { get; set; }

    public User Trainer { get; set; } = null!;
    public User Trainee { get; set; } = null!;
}
