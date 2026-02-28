using LgymApi.Domain.Enums;

namespace LgymApi.Domain.Entities;

public sealed class OutboxDelivery : EntityBase
{
    public Guid EventId { get; set; }
    public string HandlerName { get; set; } = string.Empty;
    public OutboxDeliveryStatus Status { get; set; } = OutboxDeliveryStatus.Pending;
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? LastError { get; set; }
    public OutboxMessage Event { get; set; } = null!;
}
