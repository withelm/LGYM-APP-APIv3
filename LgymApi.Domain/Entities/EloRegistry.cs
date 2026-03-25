using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class EloRegistry : EntityBase<EloRegistry>
{
    public Id<User> UserId { get; set; }
    public DateTimeOffset Date { get; set; }
    public Elo Elo { get; set; }
    public Id<Training>? TrainingId { get; set; }

    public User? User { get; set; }
    public Training? Training { get; set; }
}
