namespace LgymApi.Domain.Entities;

public sealed class EloRegistry : EntityBase
{
    public Guid UserId { get; set; }
    public DateTimeOffset Date { get; set; }
    public int Elo { get; set; }
    public Guid? TrainingId { get; set; }

    public User? User { get; set; }
    public Training? Training { get; set; }
}
