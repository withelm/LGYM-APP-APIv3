using LgymApi.Domain.Enums;

namespace LgymApi.Domain.Entities;

public sealed class OutboxMessage : EntityBase
{
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? LastError { get; set; }
    public ICollection<OutboxDelivery> Deliveries { get; set; } = new List<OutboxDelivery>();
}
