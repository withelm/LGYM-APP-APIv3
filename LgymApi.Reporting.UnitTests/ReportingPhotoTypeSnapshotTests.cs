using System.Globalization;
using System.Text.Json;
using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Options;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Reporting.Contracts.BackgroundCommands;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ReportingPhotoTypeSnapshotTests
{
    [Test]
    public async Task RenameToExactSystemKey_RemainsLiteralUntilExplicitReset()
    {
        var trainer = ReportingTestData.Account(Id<User>.New(), true);
        var type = new TrainerPhotoTypePersistenceModel(Id<TrainerPhotoType>.New(), trainer.Id, "Front", "Front", PhotoTypeNameMode.SystemLocalized, false, DateTimeOffset.UtcNow);
        var persistence = Substitute.For<IReportTemplatePersistence>();
        persistence.FindPhotoTypeAsync(type.Id, Arg.Any<CancellationToken>()).Returns(type);
        persistence.ListPhotoTypesAsync(trainer.Id, Arg.Any<CancellationToken>()).Returns([type]);
        var result = await CreateService(persistence).UpdatePhotoTypeAsync(trainer, type.Id, "Front", null);
        Assert.That(result.Value.NameMode, Is.EqualTo(PhotoTypeNameMode.TrainerProvided));
        Assert.That(result.Value.Labels, Is.EqualTo(new PhotoTypeLabelsResult("Front", "Front")));
    }

    [Test]
    public async Task ResetName_WhenLocalizedDefaultConflicts_RejectsWithoutUpdating()
    {
        var trainer = ReportingTestData.Account(Id<User>.New(), true);
        var type = new TrainerPhotoTypePersistenceModel(Id<TrainerPhotoType>.New(), trainer.Id, "Front", "Custom front", PhotoTypeNameMode.TrainerProvided, false, DateTimeOffset.UtcNow);
        var conflict = new TrainerPhotoTypePersistenceModel(Id<TrainerPhotoType>.New(), trainer.Id, null, "Przód", PhotoTypeNameMode.TrainerProvided, false, DateTimeOffset.UtcNow);
        var persistence = Substitute.For<IReportTemplatePersistence>();
        persistence.FindPhotoTypeAsync(type.Id, Arg.Any<CancellationToken>()).Returns(type);
        persistence.ListPhotoTypesAsync(trainer.Id, Arg.Any<CancellationToken>()).Returns([type, conflict]);
        var result = await CreateService(persistence).ResetPhotoTypeNameAsync(trainer, type.Id);
        Assert.That(result.IsFailure, Is.True);
        await persistence.DidNotReceiveWithAnyArgs().UpdatePhotoTypeAsync(default, default!, default!, default, default);
    }

    [Test]
    public async Task PhotoTypeRenameArchiveAndReset_PreserveLifecycleAndLocalizedLabels()
    {
        var trainer = ReportingTestData.Account(Id<User>.New(), true);
        var photoType = new TrainerPhotoTypePersistenceModel(
            Id<TrainerPhotoType>.New(), trainer.Id, "Front", "Front", PhotoTypeNameMode.SystemLocalized, false, DateTimeOffset.UtcNow);
        var persistence = Substitute.For<IReportTemplatePersistence>();
        persistence.FindPhotoTypeAsync(photoType.Id, Arg.Any<CancellationToken>()).Returns(_ => photoType);
        persistence.ListPhotoTypesAsync(trainer.Id, Arg.Any<CancellationToken>()).Returns(_ => [photoType]);
        persistence.UpdatePhotoTypeAsync(photoType.Id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<PhotoTypeNameMode>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                photoType = photoType with
                {
                    Name = call.ArgAt<string>(1),
                    NameMode = call.ArgAt<PhotoTypeNameMode>(3),
                    IsArchived = call.ArgAt<bool>(4)
                };
                return Task.CompletedTask;
            });
        var service = CreateService(persistence);

        var renamed = await service.UpdatePhotoTypeAsync(trainer, photoType.Id, "  Check-in front  ", null);
        var archived = await service.UpdatePhotoTypeAsync(trainer, photoType.Id, null, true);
        var reset = await service.ResetPhotoTypeNameAsync(trainer, photoType.Id);

        Assert.That(renamed.Value.Name, Is.EqualTo("Check-in front"));
        Assert.That(renamed.Value.NameMode, Is.EqualTo(PhotoTypeNameMode.TrainerProvided));
        Assert.That(archived.Value.NameMode, Is.EqualTo(PhotoTypeNameMode.TrainerProvided));
        Assert.That(archived.Value.IsArchived, Is.True);
        Assert.That(reset.Value.NameMode, Is.EqualTo(PhotoTypeNameMode.SystemLocalized));
        Assert.That(reset.Value.IsArchived, Is.True);
        Assert.That(reset.Value.Labels, Is.EqualTo(new PhotoTypeLabelsResult("Przód", "Front")));
    }

    [Test]
    public async Task CreateReportRequest_FreezesSchema2PhotoMetadataAndReturnsSnapshotImmediately()
    {
        var trainer = ReportingTestData.Account(Id<User>.New(), true);
        var traineeId = Id<AccountReference>.New();
        var photoTypeId = Id<TrainerPhotoType>.New();
        var template = CreateTemplate(trainer.Id, photoTypeId);
        var photoType = new TrainerPhotoTypePersistenceModel(
            photoTypeId, trainer.Id, "Front", "Front", PhotoTypeNameMode.SystemLocalized, false, DateTimeOffset.UtcNow);
        NewReportRequestPersistenceModel? staged = null;
        var templates = Substitute.For<IReportTemplatePersistence>();
        templates.FindByIdAsync(template.Id, Arg.Any<CancellationToken>()).Returns(template);
        templates.ListPhotoTypesAsync(trainer.Id, Arg.Any<CancellationToken>()).Returns([photoType]);
        var requests = Substitute.For<IReportRequestSubmissionPersistence>();
        requests.AddRequestAsync(Arg.Do<NewReportRequestPersistenceModel>(request => staged = request), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var relationships = Substitute.For<IReportingRelationshipAccessPersistence>();
        var activeEpisode = new ReportingActiveRelationshipFact(Id<CoachingEpisode>.New(), 0);
        relationships.GetAccessAsync(trainer.Id, traineeId, Arg.Any<CancellationToken>())
            .Returns(new ReportingRelationshipAccessFact(true, [], activeEpisode));
        relationships.StageActiveEpisodeRevisionCheckAsync(activeEpisode, Arg.Any<CancellationToken>())
            .Returns(true);
        var service = CreateService(templates, requests, relationships);

        var result = await service.CreateReportRequestAsync(trainer, traineeId, new CreateReportRequestCommand { TemplateId = template.Id });

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.DefinitionSnapshotProvenance, Is.EqualTo("Issued"));
        Assert.That(staged, Is.Not.Null);
        using var snapshot = JsonDocument.Parse(staged!.DefinitionSnapshotJson!);
        var moduleConfig = snapshot.RootElement.GetProperty("fields")[0].GetProperty("moduleConfig");
        Assert.That(moduleConfig.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(2));
        var frozen = moduleConfig.GetProperty("photoTypes")[0];
        Assert.That(frozen.GetProperty("photoTypeId").GetString(), Is.EqualTo(photoTypeId.ToString()));
        Assert.That(Id<PhotoUploadSession>.TryParse(frozen.GetProperty("reportPhotoSlotId").GetString() ?? string.Empty, out _), Is.True);
        Assert.That(frozen.GetProperty("labels").GetProperty("pl").GetString(), Is.EqualTo("Przód"));
        Assert.That(result.Value.Template.Fields[0].ModuleConfig!.Value.GetProperty("photoTypes")[0]
            .GetProperty("labels").GetProperty("en").GetString(), Is.EqualTo("Front"));
    }

    [Test]
    public async Task CreateReportRequest_WhenReferencedPhotoTypeIsArchived_IsBlocked()
    {
        var trainer = ReportingTestData.Account(Id<User>.New(), true);
        var traineeId = Id<AccountReference>.New();
        var photoTypeId = Id<TrainerPhotoType>.New();
        var template = CreateTemplate(trainer.Id, photoTypeId);
        var templates = Substitute.For<IReportTemplatePersistence>();
        templates.FindByIdAsync(template.Id, Arg.Any<CancellationToken>()).Returns(template);
        templates.ListPhotoTypesAsync(trainer.Id, Arg.Any<CancellationToken>()).Returns([
            new TrainerPhotoTypePersistenceModel(photoTypeId, trainer.Id, "Front", "Front", PhotoTypeNameMode.SystemLocalized, true, DateTimeOffset.UtcNow)
        ]);
        var relationships = Substitute.For<IReportingRelationshipAccessPersistence>();
        relationships.GetAccessAsync(trainer.Id, traineeId, Arg.Any<CancellationToken>())
            .Returns(new ReportingRelationshipAccessFact(true));
        var requests = Substitute.For<IReportRequestSubmissionPersistence>();
        var service = CreateService(templates, requests, relationships);

        var result = await service.CreateReportRequestAsync(trainer, traineeId, new CreateReportRequestCommand { TemplateId = template.Id });

        Assert.That(result.IsFailure, Is.True);
        await requests.DidNotReceive().AddRequestAsync(Arg.Any<NewReportRequestPersistenceModel>(), Arg.Any<CancellationToken>());
    }

    private static ReportTemplatePersistenceModel CreateTemplate(Id<AccountReference> trainerId, Id<TrainerPhotoType> photoTypeId)
        => new(
            Id<ReportTemplate>.New(),
            trainerId,
            "Original template",
            "Frozen description",
            DateTimeOffset.UtcNow,
            false,
            [new ReportTemplateFieldPersistenceModel(
                Id<ReportTemplateField>.New(),
                "photos",
                "Progress photos",
                ReportFieldType.Photos,
                true,
                1,
                JsonSerializer.Serialize(new { schemaVersion = 2, requiredPhotoTypeIds = new[] { photoTypeId.ToString() } }),
                DateTimeOffset.UtcNow)]);

    private static ReportingService CreateService(
        IReportTemplatePersistence templates,
        IReportRequestSubmissionPersistence? requests = null,
        IReportingRelationshipAccessPersistence? relationships = null)
        => new(
            templates,
            requests ?? Substitute.For<IReportRequestSubmissionPersistence>(),
            Substitute.For<IRecurringReportAssignmentPersistence>(),
            Substitute.For<IReportPhotoPersistence>(),
            relationships ?? Substitute.For<IReportingRelationshipAccessPersistence>(),
            ReportingAuthorizationTestFactory.Create(),
            Substitute.For<IAccountLookupService>(),
            new ReportSubmissionAcceptedProgressCommandFactory(),
            Substitute.For<ICommandDispatcher>(),
            Substitute.For<ICommandOutboxWriter>(),
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IPhotoStorageProvider>(),
            ReportingTestData.Mapper(),
            Substitute.For<ILogger<ReportingService>>(),
            new PhotoStorageOptions());
}
