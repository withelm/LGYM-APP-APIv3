using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;

namespace LgymApi.Domain.Entities;

public abstract class OutboxMessage<TType> : EntityBase
    where TType : notnull
{
    public TType Type { get; set; } = default!;
    public string PayloadJson { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? LastError { get; set; }
    public ICollection<OutboxDelivery> Deliveries { get; set; } = new List<OutboxDelivery>();
}

public sealed class OutboxMessage : OutboxMessage<OutboxEventType>
{
}
