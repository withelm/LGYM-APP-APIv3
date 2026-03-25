using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class UserRole
{
    public Id<User> UserId { get; set; }
    public Id<Role> RoleId { get; set; }

    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
