using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Reporting.PhotoTypesMigration;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Tests;

public sealed partial class ReportingPhotoTypesMigrationTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task Startup_ReferencedLegacyTypesRemainActive_RegardlessOfRequiredness(bool isRequired)
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var field = await fixture.Context.ReportTemplateFields.SingleAsync();
        field.IsRequired = isRequired;
        field.ModuleConfig = "{\"requiredViews\":[\"Front\",\"Side\",\"Back\",\"SideLeft\",\"SideRight\"]}";
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        await ReportingPhotoTypesStartupMigration.ApplyAsync(fixture.Context);

        var types = await fixture.Context.TrainerPhotoTypes.AsNoTracking().ToListAsync();
        types.Select(type => type.SystemKey).Should().BeEquivalentTo(Enum.GetNames<PhotoViewType>());
        types.Where(type => type.Id != fixture.ExistingFrontType.Id).Should().OnlyContain(type => !type.IsArchived);
        var side = types.Single(type => type.SystemKey == "Side");
        side.Name.Should().Be("Side");
        side.NameMode.Should().Be(PhotoTypeNameMode.SystemLocalized);
        // Migration must not undo a pre-existing trainer archive/rename choice.
        var existing = types.Single(type => type.Id == fixture.ExistingFrontType.Id);
        existing.IsArchived.Should().BeTrue();
        existing.Name.Should().Be("Coach check-in front");
        existing.NameMode.Should().Be(PhotoTypeNameMode.TrainerProvided);
        var snapshot = await fixture.Context.ReportRequestDefinitionSnapshots.AsNoTracking().SingleAsync();
        using var document = System.Text.Json.JsonDocument.Parse(snapshot.DefinitionJson);
        var frozenSide = document.RootElement.GetProperty("fields")[0].GetProperty("moduleConfig")
            .GetProperty("photoTypes").EnumerateArray().Single(type => type.GetProperty("systemKey").GetString() == "Side");
        frozenSide.GetProperty("labels").GetProperty("pl").GetString().Should().Be("Bok");
        frozenSide.GetProperty("labels").GetProperty("en").GetString().Should().Be("Side");

        await ReportingPhotoTypesStartupMigration.ApplyAsync(fixture.Context);

        (await fixture.Context.ReportRequestDefinitionSnapshots.AsNoTracking().SingleAsync())
            .DefinitionJson.Should().Be(snapshot.DefinitionJson);
        (await fixture.Context.TrainerPhotoTypes.CountAsync()).Should().Be(5);
    }

    [TestCase(false, "live", false)]
    [TestCase(true, "live", false)]
    [TestCase(false, "none", true)]
    [TestCase(true, "none", true)]
    [TestCase(true, "deleted-template", true)]
    [TestCase(true, "deleted-field", true)]
    [TestCase(true, "other-trainer", true)]
    [TestCase(true, "other-type", true)]
    [TestCase(true, "non-photo", true)]
    [TestCase(true, "malformed-config", true)]
    public async Task Apply_ArchiveStateUsesAllSurvivingTemplatesOfTheOriginalTrainer(
        bool historicalFieldRequired, string reference, bool expectedArchived)
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var historicalTemplate = await fixture.Context.ReportTemplates.SingleAsync();
        historicalTemplate.IsDeleted = true;
        var historicalField = await fixture.Context.ReportTemplateFields.SingleAsync();
        historicalField.IsRequired = historicalFieldRequired;

        if (reference != "none")
        {
            var template = new ReportTemplate
            {
                Id = Id<ReportTemplate>.New(),
                TrainerId = reference == "other-trainer" ? fixture.Trainee.Id : fixture.Trainer.Id,
                Name = "Template with no issued requests",
                IsDeleted = reference == "deleted-template"
            };
            fixture.Context.ReportTemplates.Add(template);
            fixture.Context.ReportTemplateFields.Add(new ReportTemplateField
            {
                Id = Id<ReportTemplateField>.New(),
                TemplateId = template.Id,
                Key = "photos",
                Label = "Optional progress photos",
                Type = reference == "non-photo" ? ReportFieldType.Text : ReportFieldType.Photos,
                IsRequired = false,
                Order = 1,
                IsDeleted = reference == "deleted-field",
                ModuleConfig = reference switch
                {
                    "other-type" => "{\"requiredViews\":[\"Side\"]}",
                    "malformed-config" => "not-json",
                    // Match legacy spellings without changing the canonical SystemKey.
                    _ => "{\"requiredViews\":[\"back\"]}"
                }
            });
        }
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var preserved = await fixture.CapturePreservedValuesAsync();
        var runner = new ReportingPhotoTypesMigrationRunner(fixture.Context);
        var preview = await runner.RunAsync(Options(ReportingPhotoTypesMigrationMode.DryRun), CancellationToken.None);

        preview.HasUnresolvedRequests.Should().BeFalse();
        preview.Counters.DefaultsPlanned.Should().Be(1);
        (await fixture.Context.TrainerPhotoTypes.CountAsync()).Should().Be(1, "dry-run must not create definitions");
        var applied = await runner.RunAsync(
            Options(ReportingPhotoTypesMigrationMode.Apply, preview.Fingerprint), CancellationToken.None);

        applied.Applied.Should().BeTrue();
        applied.HasUnresolvedRequests.Should().BeFalse();
        var back = await fixture.Context.TrainerPhotoTypes.AsNoTracking().SingleAsync(type => type.SystemKey == "Back");
        back.TrainerId.Should().Be(fixture.Trainer.Id);
        back.IsArchived.Should().Be(expectedArchived);
        await fixture.AssertPreservedValuesAsync(preserved);
        var snapshot = await fixture.Context.ReportRequestDefinitionSnapshots.AsNoTracking().SingleAsync();
        var nextPreview = await runner.RunAsync(Options(ReportingPhotoTypesMigrationMode.DryRun), CancellationToken.None);
        var rerun = await runner.RunAsync(
            Options(ReportingPhotoTypesMigrationMode.Apply, nextPreview.Fingerprint), CancellationToken.None);

        rerun.Counters.DefaultsWritten.Should().Be(0);
        rerun.Counters.SnapshotsWritten.Should().Be(0);
        (await fixture.Context.ReportRequestDefinitionSnapshots.AsNoTracking().SingleAsync())
            .DefinitionJson.Should().Be(snapshot.DefinitionJson);
    }

    [Test]
    public async Task Apply_RetainedDeletedTrainer_KeepsNewTypesArchivedWithoutRestoringAccount()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        var trainer = await fixture.Context.Users.SingleAsync(user => user.Id == fixture.Trainer.Id);
        trainer.IsDeleted = true;
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var runner = new ReportingPhotoTypesMigrationRunner(fixture.Context);
        var preview = await runner.RunAsync(
            Options(ReportingPhotoTypesMigrationMode.DryRun, allowRetainedDeletedOwners: true), CancellationToken.None);

        var applied = await runner.RunAsync(
            Options(ReportingPhotoTypesMigrationMode.Apply, preview.Fingerprint, true), CancellationToken.None);

        applied.Applied.Should().BeTrue();
        (await fixture.Context.TrainerPhotoTypes.SingleAsync(type => type.SystemKey == "Back"))
            .IsArchived.Should().BeTrue();
        (await fixture.Context.Users.IgnoreQueryFilters().SingleAsync(user => user.Id == fixture.Trainer.Id))
            .IsDeleted.Should().BeTrue();
    }
}
