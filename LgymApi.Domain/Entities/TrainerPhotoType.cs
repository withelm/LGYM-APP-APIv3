using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class TrainerPhotoType : EntityBase<TrainerPhotoType>
{
    public Id<User> TrainerId { get; set; }
    public string? SystemKey { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public PhotoTypeNameMode NameMode { get; set; }
    public bool IsArchived { get; set; }
    public User Trainer { get; set; } = null!;
}
