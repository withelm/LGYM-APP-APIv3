using System.Text.Json;
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

        Assert.That(effective.Name, Is.EqualTo("Frozen name"));
        var assertedValue75 = (effective.Fields).ToArray();
        Assert.That(assertedValue75, Has.Exactly(1).Items);
        Assert.That(assertedValue75.Single().Type, Is.EqualTo(ReportFieldType.Photos));
        Assert.That(effective.Fields[0].Label, Is.EqualTo("Frozen label"));
        Assert.That(request.DefinitionSnapshot.DefinitionJson, Is.EqualTo(json));
    }

    [Test]
    public void Serialize_WritesCanonicalPropertyNamesAndStringEnums()
    {
        var document = new ReportTemplateSnapshotDocument(Id<ReportTemplate>.New().ToString(), "Name", null,
            [new ReportTemplateSnapshotFieldDocument("photos", "Label", ReportFieldType.Photos, true, 0, null)], "Issued");

        using var json = JsonDocument.Parse(ReportTemplateSnapshotFactory.Serialize(document));

        Assert.That(json.RootElement.GetProperty("fields")[0].GetProperty("type").GetString(), Is.EqualTo("Photos"));
        Assert.That(json.RootElement.GetProperty("provenance").GetString(), Is.EqualTo("Issued"));
    }
}
