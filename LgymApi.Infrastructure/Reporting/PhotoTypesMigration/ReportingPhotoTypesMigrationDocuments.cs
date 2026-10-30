using System.Text.Json;
using System.Text.Json.Serialization;
using LgymApi.Domain.Enums;

namespace LgymApi.Infrastructure.Reporting.PhotoTypesMigration;

internal sealed record MigrationReportTemplateSnapshotDocument(
    string TemplateId,
    string Name,
    string? Description,
    IReadOnlyList<MigrationReportTemplateSnapshotFieldDocument> Fields,
    string Provenance,
    int SchemaVersion = 2);

internal sealed record MigrationReportTemplateSnapshotFieldDocument(
    string Key,
    string Label,
    ReportFieldType Type,
    bool IsRequired,
    int Order,
    JsonElement? ModuleConfig);

internal sealed record MigrationPhotoModuleConfig(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("requiredPhotoTypeIds")] IReadOnlyList<string> RequiredPhotoTypeIds,
    [property: JsonPropertyName("photoTypes")] IReadOnlyList<MigrationPhotoBindingDocument> PhotoTypes);

internal sealed record MigrationPhotoBindingDocument(
    [property: JsonPropertyName("photoTypeId")] string PhotoTypeId,
    [property: JsonPropertyName("reportPhotoSlotId")] string ReportPhotoSlotId,
    [property: JsonPropertyName("systemKey")] string? SystemKey,
    [property: JsonPropertyName("nameMode")] PhotoTypeNameMode NameMode,
    [property: JsonPropertyName("labels")] MigrationPhotoTypeLabels Labels);

internal sealed record MigrationPhotoTypeLabels(
    [property: JsonPropertyName("pl")] string Pl,
    [property: JsonPropertyName("en")] string En);
