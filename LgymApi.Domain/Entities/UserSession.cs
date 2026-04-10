using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class UserSession : EntityBase<UserSession>
{
    public Id<User> UserId { get; set; }
    public Guid Jti { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
