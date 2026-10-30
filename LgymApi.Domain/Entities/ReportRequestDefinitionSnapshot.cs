using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class ReportRequestDefinitionSnapshot : EntityBase<ReportRequestDefinitionSnapshot>
{
    public Id<ReportRequest> ReportRequestId { get; set; }
    public Id<ReportTemplate> SourceTemplateId { get; set; }
    public string DefinitionJson { get; set; } = string.Empty;
    public string Provenance { get; set; } = "Issued";
    public ReportRequest ReportRequest { get; set; } = null!;
}
