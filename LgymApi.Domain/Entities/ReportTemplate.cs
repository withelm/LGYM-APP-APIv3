using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class ReportTemplate : EntityBase<ReportTemplate>
{
    public Id<User> TrainerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public User Trainer { get; set; } = null!;
    public ICollection<ReportTemplateField> Fields { get; set; } = new List<ReportTemplateField>();
}
