using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class Gym : EntityBase<Gym>
{
    public Id<User> UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Id<Address>? AddressId { get; set; }

    public User? User { get; set; }
    public Address? Address { get; set; }
    public ICollection<Training> Trainings { get; set; } = new List<Training>();
}
