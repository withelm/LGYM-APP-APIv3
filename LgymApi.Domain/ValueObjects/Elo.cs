namespace LgymApi.Domain.ValueObjects;

public readonly record struct Elo
{
    public int Value { get; }

    public Elo(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Elo cannot be negative.");
        }

        Value = value;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator int(Elo elo) => elo.Value;

    public static implicit operator Elo(int value) => new(value);
}
