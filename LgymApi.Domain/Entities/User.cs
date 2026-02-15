using LgymApi.Domain.Enums;

namespace LgymApi.Domain.Entities;

public sealed class User : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid? PlanId { get; set; }
    public string ProfileRank { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsVisibleInRanking { get; set; } = true;

    public string? LegacyHash { get; set; }
    public string? LegacySalt { get; set; }
    public int? LegacyIterations { get; set; }
    public int? LegacyKeyLength { get; set; }
    public string? LegacyDigest { get; set; }

    public Plan? Plan { get; set; }
    public ICollection<Plan> Plans { get; set; } = new List<Plan>();
    public ICollection<Exercise> Exercises { get; set; } = new List<Exercise>();
    public ICollection<Training> Trainings { get; set; } = new List<Training>();
    public ICollection<Measurement> Measurements { get; set; } = new List<Measurement>();
    public ICollection<MainRecord> MainRecords { get; set; } = new List<MainRecord>();
    public ICollection<EloRegistry> EloRegistries { get; set; } = new List<EloRegistry>();
    public ICollection<Gym> Gyms { get; set; } = new List<Gym>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
