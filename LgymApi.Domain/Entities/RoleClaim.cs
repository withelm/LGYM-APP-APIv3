using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class RoleClaim : EntityBase<RoleClaim>
{
    public Id<Role> RoleId { get; set; }
    public string ClaimType { get; set; } = string.Empty;
    public string ClaimValue { get; set; } = string.Empty;

    public Role Role { get; set; } = null!;
}
