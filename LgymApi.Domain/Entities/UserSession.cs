using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class UserSession : EntityBase<UserSession>
{
    public Id<User> UserId { get; set; }
    public string Jti { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
