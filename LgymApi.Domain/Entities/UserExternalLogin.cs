using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class UserExternalLogin : EntityBase<UserExternalLogin>
{
    public Id<User> UserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderKey { get; set; } = string.Empty;
    public string? ProviderEmail { get; set; }

    public User? User { get; set; }
}
