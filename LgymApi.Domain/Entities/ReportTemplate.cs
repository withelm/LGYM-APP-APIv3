namespace LgymApi.Domain.Entities;

public sealed class ReportTemplate : EntityBase
{
    public Guid TrainerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDeleted { get; set; }

    public User Trainer { get; set; } = null!;
    public ICollection<ReportTemplateField> Fields { get; set; } = new List<ReportTemplateField>();
}
