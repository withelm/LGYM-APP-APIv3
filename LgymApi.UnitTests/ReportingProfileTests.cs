using LgymApi.Domain.ValueObjects;
using LgymApi.Domain.Entities;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Mapping.Profiles;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Enums;
using LgymApi.Identity.Contracts;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ReportingProfileTests
{
    [Test]
    public void ReportingProfile_MapsTemplateRequestAndSubmission_WithExpectedValues()
    {
        var mapper = CreateMapper(new ReportingProfile());

        var templateResult = new ReportTemplateResult
        {
            Id = Id<LgymApi.Domain.Entities.ReportTemplate>.New(),
            TrainerId = Id<AccountReference>.New(),
            Name = "Weekly",
            Fields =
            [
                new ReportTemplateFieldResult { Key = "weight", Label = "Weight", Type = ReportFieldType.Number, IsRequired = true, Order = 0 }
            ]
        };

        var templateDto = mapper.Map<ReportTemplateResult, ReportTemplateDto>(templateResult);
        Assert.That(templateDto.Fields, Has.Exactly(1).Items);

        var requestResult = new ReportRequestResult
        {
            Id = Id<LgymApi.Domain.Entities.ReportRequest>.New(),
            TrainerId = Id<AccountReference>.New(),
            TraineeId = Id<AccountReference>.New(),
            TemplateId = Id<LgymApi.Domain.Entities.ReportTemplate>.New(),
            Status = ReportRequestStatus.Pending,
            Template = templateResult
        };

        var requestDto = mapper.Map<ReportRequestResult, ReportRequestDto>(requestResult);
        Assert.That(requestDto.Template, Is.Not.Null);

        var submissionResult = new ReportSubmissionResult
        {
            Id = Id<LgymApi.Domain.Entities.ReportSubmission>.New(),
            ReportRequestId = Id<LgymApi.Domain.Entities.ReportRequest>.New(),
            TraineeId = Id<AccountReference>.New(),
            Answers = new Dictionary<string, JsonElement>(),
            TrainerOverallComment = "Overall coach feedback",
            TrainerFieldComments = new Dictionary<string, string> { ["weight"] = "Add more context" },
            Request = requestResult
        };

        var submissionDto = mapper.Map<ReportSubmissionResult, ReportSubmissionDto>(submissionResult);
        Assert.That(submissionDto.Answers, Is.Empty);
        Assert.That(submissionDto.Request, Is.Not.Null);
        Assert.That(submissionDto.TrainerOverallComment, Is.EqualTo("Overall coach feedback"));
        Assert.That(submissionDto.TrainerFieldComments["weight"], Is.EqualTo("Add more context"));
    }

    [Test]
    public void ReportingProfile_MapsSubmissionAnswers_AsProvidedDictionary()
    {
        var mapper = CreateMapper(new ReportingProfile());

        var submissionResult = new ReportSubmissionResult
        {
            Id = Id<LgymApi.Domain.Entities.ReportSubmission>.New(),
            ReportRequestId = Id<LgymApi.Domain.Entities.ReportRequest>.New(),
            TraineeId = Id<AccountReference>.New(),
            Answers = new Dictionary<string, JsonElement>
            {
                ["Weight"] = JsonSerializer.SerializeToElement(82)
            },
            Request = new ReportRequestResult
            {
                Id = Id<LgymApi.Domain.Entities.ReportRequest>.New(),
                TrainerId = Id<AccountReference>.New(),
                TraineeId = Id<AccountReference>.New(),
                TemplateId = Id<LgymApi.Domain.Entities.ReportTemplate>.New(),
                Status = ReportRequestStatus.Submitted,
                Template = new ReportTemplateResult()
            }
        };

        var dto = mapper.Map<ReportSubmissionResult, ReportSubmissionDto>(submissionResult);
        Assert.That(dto.Answers.ContainsKey("Weight"), Is.True);
    }

    [Test]
    public void ReportingProfile_MapsCompletedInternalRequestToLegacySubmittedStatus()
    {
        var mapper = CreateMapper(new ReportingProfile());

        var dto = mapper.Map<ReportRequestResult, ReportRequestDto>(new ReportRequestResult
        {
            Status = ReportRequestStatus.Completed,
            Template = new ReportTemplateResult()
        });

        Assert.That(dto.Status, Is.EqualTo(ReportRequestStatus.Submitted));
    }

    [Test]
    public void ReportingProfile_MapsPhotoDtosAndNullSources_WithSafeDefaults()
    {
        var mapper = CreateMapper(new ReportingProfile());

        var uploadDto = mapper.Map<InitiatePhotoUploadResult, InitiatePhotoUploadResponse>(new InitiatePhotoUploadResult
        {
            UploadUrl = "https://upload.example.com",
            StorageKey = "photos/key.jpg",
            ExpiresAt = DateTimeOffset.UtcNow
        });
        Assert.That(uploadDto.UploadUrl, Is.EqualTo("https://upload.example.com"));

        var readDto = mapper.Map<SignedReadUrlResult, GetSignedReadUrlResponse>(new SignedReadUrlResult
        {
            ReadUrl = "https://read.example.com",
            ExpiresAt = DateTimeOffset.UtcNow
        });
        Assert.That(readDto.ReadUrl, Is.EqualTo("https://read.example.com"));

        var photoHistoryDto = mapper.Map<PhotoHistoryItemResult, PhotoHistoryItemResponse>(new PhotoHistoryItemResult
        {
            Id = Id<Photo>.New(),
            ViewType = "Front",
            SizeBytes = 123,
            ThumbnailUrl = "thumb",
            ReadUrl = "read",
            ReportRequestId = Id<ReportRequest>.New(),
            UploadedAt = DateTimeOffset.UtcNow
        });
        Assert.That(photoHistoryDto.ViewType, Is.EqualTo("Front"));

        Assert.That(mapper.Map<ReportTemplateResult, ReportTemplateDto>(new ReportTemplateResult()), Is.Not.Null);
        Assert.That(mapper.Map<ReportRequestResult, ReportRequestDto>(new ReportRequestResult()), Is.Not.Null);
        Assert.That(mapper.Map<ReportSubmissionResult, ReportSubmissionDto>(new ReportSubmissionResult()), Is.Not.Null);
    }

    [Test]
    public void ReportingDictionaryAndSnapshotProvenance_SerializeWithExactClientPropertyNames()
    {
        var mapper = CreateMapper(new ReportingProfile());
        var dto = mapper.Map<TrainerPhotoTypeResult, TrainerPhotoTypeDto>(new TrainerPhotoTypeResult
        {
            Id = Id<TrainerPhotoType>.New(),
            SystemKey = "Front",
            Name = "Front",
            NameMode = PhotoTypeNameMode.SystemLocalized,
            DisplayName = "Przód",
            Labels = new PhotoTypeLabelsResult("Przód", "Front")
        });
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(dto, options));

        Assert.That(json.RootElement.EnumerateObject().Select(property => property.Name), Is.EqualTo(new[] { "_id", "systemKey", "name", "nameMode", "isArchived", "displayName", "labels" }));
        Assert.That(json.RootElement.GetProperty("labels").EnumerateObject().Select(property => property.Name), Is.EqualTo(new[] { "pl", "en" }));
        Assert.That(json.RootElement.GetProperty("nameMode").GetString(), Is.EqualTo("SystemLocalized"));
    }

    private static IMapper CreateMapper(params IMappingProfile[] profiles)
    {
        return (IMapper)Activator.CreateInstance(
            typeof(Mapper),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [profiles],
            culture: null)!;
    }
}
