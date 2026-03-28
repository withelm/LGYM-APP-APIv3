using LgymApi.Domain.ValueObjects;
using LgymApi.Domain.Entities;
using System.Reflection;
using System.Text.Json;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Mapping.Profiles;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Enums;

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
             TrainerId = Id<LgymApi.Domain.Entities.User>.New(),
            Name = "Weekly",
            Fields =
            [
                new ReportTemplateFieldResult { Key = "weight", Label = "Weight", Type = ReportFieldType.Number, IsRequired = true, Order = 0 }
            ]
        };

        var templateDto = mapper.Map<ReportTemplateResult, ReportTemplateDto>(templateResult);
        Assert.That(templateDto.Fields, Has.Count.EqualTo(1));

        var requestResult = new ReportRequestResult
        {
             Id = Id<LgymApi.Domain.Entities.ReportRequest>.New(),
             TrainerId = Id<LgymApi.Domain.Entities.User>.New(),
             TraineeId = Id<LgymApi.Domain.Entities.User>.New(),
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
             TraineeId = Id<LgymApi.Domain.Entities.User>.New(),
            Answers = new Dictionary<string, JsonElement>(),
            Request = requestResult
        };

        var submissionDto = mapper.Map<ReportSubmissionResult, ReportSubmissionDto>(submissionResult);
        Assert.Multiple(() =>
        {
            Assert.That(submissionDto.Answers, Is.Empty);
            Assert.That(submissionDto.Request, Is.Not.Null);
        });
    }

    [Test]
    public void ReportingProfile_MapsSubmissionAnswers_AsProvidedDictionary()
    {
        var mapper = CreateMapper(new ReportingProfile());

        var submissionResult = new ReportSubmissionResult
        {
             Id = Id<LgymApi.Domain.Entities.ReportSubmission>.New(),
             ReportRequestId = Id<LgymApi.Domain.Entities.ReportRequest>.New(),
             TraineeId = Id<LgymApi.Domain.Entities.User>.New(),
            Answers = new Dictionary<string, JsonElement>
            {
                ["Weight"] = JsonSerializer.SerializeToElement(82)
            },
            Request = new ReportRequestResult
            {
                Id = Id<LgymApi.Domain.Entities.ReportRequest>.New(),
                TrainerId = Id<LgymApi.Domain.Entities.User>.New(),
                TraineeId = Id<LgymApi.Domain.Entities.User>.New(),
                 TemplateId = Id<LgymApi.Domain.Entities.ReportTemplate>.New(),
                Status = ReportRequestStatus.Submitted,
                Template = new ReportTemplateResult()
            }
        };

        var dto = mapper.Map<ReportSubmissionResult, ReportSubmissionDto>(submissionResult);
        Assert.That(dto.Answers.ContainsKey("Weight"), Is.True);
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
