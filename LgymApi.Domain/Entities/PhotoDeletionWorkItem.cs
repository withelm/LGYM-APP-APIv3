using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class PhotoDeletionWorkItem : EntityBase<PhotoDeletionWorkItem>
{
    public Id<CorrelationScope> DeletionGeneration { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public bool IsPrefix { get; set; }
    public string? ContinuationToken { get; set; }
    public DateTimeOffset NotBeforeUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
}
