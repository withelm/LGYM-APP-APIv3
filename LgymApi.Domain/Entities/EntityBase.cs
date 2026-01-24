namespace LgymApi.Domain.Entities;

public abstract class EntityBase
{
    public Guid Id { get; set; }
    public string? LegacyMongoId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
