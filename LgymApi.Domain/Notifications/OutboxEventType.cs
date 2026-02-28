namespace LgymApi.Domain.Notifications;

public readonly record struct OutboxEventType
{
    public string Value { get; }

    private OutboxEventType(string value)
    {
        Value = value;
    }

    public static OutboxEventType Define(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Outbox event type cannot be empty.", nameof(value));
        }

        return new OutboxEventType(value);
    }

    public static OutboxEventType Parse(string value)
    {
        if (OutboxEventTypes.TryFromValue(value, out var outboxEventType))
        {
            return outboxEventType;
        }

        return Define(value);
    }

    public override string ToString() => Value;
}
