namespace LgymApi.Domain.ValueObjects;

public readonly record struct Id<TEntity>
{
    private Guid Value { get; }

    public Id(Guid value)
    {
        Value = value;
    }

    public static Id<TEntity> New() => new(Guid.NewGuid());

    public static Id<TEntity> Empty => new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public override string ToString() => Value.ToString();

    public Guid GetValue() => Value;

    public static bool TryParse(string id, out Id<TEntity> parsedId)
    {
        if (Guid.TryParse(id, out var guid))
        {
            parsedId = new Id<TEntity>(guid);
            return true;
        }

        parsedId = Empty;
        return false;
    }

    public static explicit operator Guid(Id<TEntity> id) => id.Value;

    public static explicit operator Id<TEntity>(Guid value) => new(value);
}