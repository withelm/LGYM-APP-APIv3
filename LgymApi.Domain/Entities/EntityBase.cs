using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public abstract class EntityBase<TEntity> where TEntity : EntityBase<TEntity>
{
    public Id<TEntity> Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
