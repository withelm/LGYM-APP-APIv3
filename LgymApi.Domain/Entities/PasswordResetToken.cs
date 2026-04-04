using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class PasswordResetToken : EntityBase<PasswordResetToken>
{
    public Id<User> UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsUsed { get; set; }

    public User? User { get; set; }
}
