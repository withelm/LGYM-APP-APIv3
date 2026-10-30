using System.Globalization;
using System.Text.Json;
using FluentAssertions;
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
        result.Value.NameMode.Should().Be(PhotoTypeNameMode.TrainerProvided);
        result.Value.Labels.Should().Be(new PhotoTypeLabelsResult("Front", "Front"));
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
        result.IsFailure.Should().BeTrue();
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

        renamed.Value.Name.Should().Be("Check-in front");
        renamed.Value.NameMode.Should().Be(PhotoTypeNameMode.TrainerProvided);
        archived.Value.NameMode.Should().Be(PhotoTypeNameMode.TrainerProvided);
        archived.Value.IsArchived.Should().BeTrue();
        reset.Value.NameMode.Should().Be(PhotoTypeNameMode.SystemLocalized);
        reset.Value.IsArchived.Should().BeTrue();
        reset.Value.Labels.Should().Be(new PhotoTypeLabelsResult("Przód", "Front"));
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
        relationships.GetAccessAsync(trainer.Id, traineeId, Arg.Any<CancellationToken>())
            .Returns(new ReportingRelationshipAccessFact(true));
        var service = CreateService(templates, requests, relationships);

        var result = await service.CreateReportRequestAsync(trainer, traineeId, new CreateReportRequestCommand { TemplateId = template.Id });

        result.IsSuccess.Should().BeTrue();
        result.Value.DefinitionSnapshotProvenance.Should().Be("Issued");
        staged.Should().NotBeNull();
        using var snapshot = JsonDocument.Parse(staged!.DefinitionSnapshotJson!);
        var moduleConfig = snapshot.RootElement.GetProperty("Fields")[0].GetProperty("ModuleConfig");
        moduleConfig.GetProperty("schemaVersion").GetInt32().Should().Be(2);
        var frozen = moduleConfig.GetProperty("photoTypes")[0];
        frozen.GetProperty("photoTypeId").GetString().Should().Be(photoTypeId.ToString());
        Id<PhotoUploadSession>.TryParse(frozen.GetProperty("reportPhotoSlotId").GetString() ?? string.Empty, out _)
            .Should().BeTrue();
        frozen.GetProperty("labels").GetProperty("pl").GetString().Should().Be("Przód");
        result.Value.Template.Fields[0].ModuleConfig!.Value.GetProperty("photoTypes")[0]
            .GetProperty("labels").GetProperty("en").GetString().Should().Be("Front");
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

        result.IsFailure.Should().BeTrue();
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
            new ReportSubmissionAcceptedProgressCommandFactory(),
            Substitute.For<ICommandDispatcher>(),
            Substitute.For<ICommandOutboxWriter>(),
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IPhotoStorageProvider>(),
            ReportingTestData.Mapper(),
            Substitute.For<ILogger<ReportingService>>(),
            new PhotoStorageOptions());
}
