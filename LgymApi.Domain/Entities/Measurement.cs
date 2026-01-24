using LgymApi.Domain.Enums;

namespace LgymApi.Domain.Entities;

public sealed class Measurement : EntityBase
{
    public Guid UserId { get; set; }
    public BodyParts BodyPart { get; set; }
    public string Unit { get; set; } = string.Empty;
    public double Value { get; set; }

    public User? User { get; set; }
}
