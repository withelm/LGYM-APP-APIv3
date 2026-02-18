namespace LgymApi.Domain.Entities;

public sealed class TrainerTraineeLink : EntityBase
{
    public Guid TrainerId { get; set; }
    public Guid TraineeId { get; set; }

    public User Trainer { get; set; } = null!;
    public User Trainee { get; set; } = null!;
}
