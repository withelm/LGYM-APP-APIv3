namespace LgymApi.Domain.Entities;

public sealed class Gym : EntityBase
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? AddressId { get; set; }

    public User? User { get; set; }
    public Address? Address { get; set; }
    public ICollection<Training> Trainings { get; set; } = new List<Training>();
}
