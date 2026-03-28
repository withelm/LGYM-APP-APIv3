using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class ReportTemplateField : EntityBase<ReportTemplateField>
{
    public Id<ReportTemplate> TemplateId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public ReportFieldType Type { get; set; }
    public bool IsRequired { get; set; }
    public int Order { get; set; }

    public ReportTemplate Template { get; set; } = null!;
}
