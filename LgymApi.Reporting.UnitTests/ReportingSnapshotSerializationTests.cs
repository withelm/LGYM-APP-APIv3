using System.Text.Json;
using FluentAssertions;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.UnitTests;

public sealed class ReportingSnapshotSerializationTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void EffectiveTemplate_ReadsLegacyAndCanonicalSnapshotsWithoutRewritingBytes(bool canonical)
    {
        var request = PhotoServiceTestFactory.CreateReportRequest(Id<ReportRequest>.New(), Id<User>.New());
        var document = new ReportTemplateSnapshotDocument(request.TemplateId.ToString(), "Frozen name", "Frozen description",
            [new ReportTemplateSnapshotFieldDocument("photos", "Frozen label", ReportFieldType.Photos, true, 1,
                JsonSerializer.SerializeToElement(new { schemaVersion = 2, labels = new { pl = "Front", en = "Front" } }))], "Issued");
        var json = JsonSerializer.Serialize(document, canonical ? SharedSerializationOptions.Current : null);
        request.DefinitionSnapshot = new ReportRequestDefinitionSnapshot
        {
            Id = Id<ReportRequestDefinitionSnapshot>.New(), ReportRequestId = request.Id,
            SourceTemplateId = request.TemplateId, DefinitionJson = json, Provenance = "Issued"
        };

        var effective = ReportTemplateSnapshotFactory.EffectiveTemplate(ReportingTestData.Request(request));

        effective.Name.Should().Be("Frozen name");
        effective.Fields.Should().ContainSingle().Which.Type.Should().Be(ReportFieldType.Photos);
        effective.Fields[0].Label.Should().Be("Frozen label");
        request.DefinitionSnapshot.DefinitionJson.Should().Be(json);
    }

    [Test]
    public void Serialize_WritesCanonicalPropertyNamesAndStringEnums()
    {
        var document = new ReportTemplateSnapshotDocument(Id<ReportTemplate>.New().ToString(), "Name", null,
            [new ReportTemplateSnapshotFieldDocument("photos", "Label", ReportFieldType.Photos, true, 0, null)], "Issued");

        using var json = JsonDocument.Parse(ReportTemplateSnapshotFactory.Serialize(document));

        json.RootElement.GetProperty("fields")[0].GetProperty("type").GetString().Should().Be("Photos");
        json.RootElement.GetProperty("provenance").GetString().Should().Be("Issued");
    }
}
