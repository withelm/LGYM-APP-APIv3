using System.Net.Mail;

namespace LgymApi.Domain.ValueObjects;

public readonly record struct Email
{
    public string Value { get; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Value = string.Empty;
            return;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (!MailAddress.TryCreate(normalized, out _))
        {
            throw new ArgumentException("Email must be a valid address.", nameof(value));
        }

        Value = normalized;
    }

    public override string ToString() => Value;

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public static implicit operator string(Email email) => email.Value;

    public static implicit operator Email(string value) => new(value);
}
