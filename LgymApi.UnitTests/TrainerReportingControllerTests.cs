using FluentAssertions;
using LgymApi.Api;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Features.Trainer.Controllers;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TrainerReportingControllerTests
{
    [Test]
    public async Task GetTemplate_WithInvalidId_ReturnsBadRequest()
    {
        var controller = CreateController(Substitute.For<IReportingService>());

        var result = await controller.GetTemplate("bad-id");

        result.Should().BeAssignableTo<ObjectResult>();
        ((ObjectResult)result).StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Test]
    public async Task CreateTemplate_WithValidRequest_ForwardsMappedFieldsAndReturnsCreated()
    {
        var service = Substitute.For<IReportingService>();
        CreateReportTemplateCommand? captured = null;
        var templateResult = CreateTemplateResult();
        service.CreateTemplateAsync(Arg.Any<User>(), Arg.Do<CreateReportTemplateCommand>(cmd => captured = cmd), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ReportTemplateResult, AppError>(templateResult));
        var controller = CreateController(service);

        var result = await controller.CreateTemplate(new UpsertReportTemplateRequest
        {
            Name = "Weekly",
            Description = "Desc",
            Fields =
            [
                new ReportTemplateFieldRequest { Key = "weight", Label = "Weight", Type = ReportFieldType.Number, IsRequired = true, Order = 1 }
            ]
        });

        result.Should().BeOfType<ObjectResult>();
        ((ObjectResult)result).StatusCode.Should().Be(StatusCodes.Status201Created);
        captured.Should().NotBeNull();
        captured!.Fields.Should().ContainSingle();
        captured.Fields[0].Key.Should().Be("weight");
    }

    [Test]
    public async Task GetTemplates_ReturnsMappedDtos()
    {
        var service = Substitute.For<IReportingService>();
        service.GetTrainerTemplatesAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<List<ReportTemplateResult>, AppError>([CreateTemplateResult()]));
        var controller = CreateController(service);

        var result = await controller.GetTemplates();

        var payload = ((OkObjectResult)result).Value.Should().BeAssignableTo<IEnumerable<ReportTemplateDto>>().Subject;
        payload.Should().ContainSingle();
    }

    [Test]
    public async Task UpdateTemplate_WithValidRequest_ForwardsParsedId()
    {
        var service = Substitute.For<IReportingService>();
        var templateId = Id<ReportTemplate>.New();
        Id<ReportTemplate> capturedId = Id<ReportTemplate>.Empty;
        service.UpdateTemplateAsync(Arg.Any<User>(), Arg.Do<Id<ReportTemplate>>(id => capturedId = id), Arg.Any<CreateReportTemplateCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ReportTemplateResult, AppError>(CreateTemplateResult(templateId)));
        var controller = CreateController(service);

        var result = await controller.UpdateTemplate(templateId.ToString(), new UpsertReportTemplateRequest { Name = "Weekly", Fields = [] });

        result.Should().BeOfType<OkObjectResult>();
        capturedId.Should().Be(templateId);
    }

    [Test]
    public async Task DeleteTemplate_WithValidId_ReturnsDeletedMessage()
    {
        var service = Substitute.For<IReportingService>();
        service.DeleteTemplateAsync(Arg.Any<User>(), Arg.Any<Id<ReportTemplate>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<Unit, AppError>(Unit.Value));
        var controller = CreateController(service);

        var result = await controller.DeleteTemplate(Id<ReportTemplate>.New().ToString());

        result.Should().BeOfType<OkObjectResult>();
    }

    [Test]
    public async Task CreateReportRequest_WithInvalidIds_ReturnsBadRequest()
    {
        var controller = CreateController(Substitute.For<IReportingService>());

        (await controller.CreateReportRequest("bad-user", new CreateReportRequestRequest { TemplateId = Id<ReportTemplate>.New().ToString() }))
            .Should().BeAssignableTo<ObjectResult>();
        (await controller.CreateReportRequest(Id<User>.New().ToString(), new CreateReportRequestRequest { TemplateId = "bad-template" }))
            .Should().BeAssignableTo<ObjectResult>();
    }

    [Test]
    public async Task CreateReportRequest_WithValidRequest_ForwardsParsedIds()
    {
        var service = Substitute.For<IReportingService>();
        var traineeId = Id<User>.New();
        var templateId = Id<ReportTemplate>.New();
        Id<User> capturedTraineeId = Id<User>.Empty;
        CreateReportRequestCommand? capturedCommand = null;
        service.CreateReportRequestAsync(Arg.Any<User>(), Arg.Do<Id<User>>(id => capturedTraineeId = id), Arg.Do<CreateReportRequestCommand>(cmd => capturedCommand = cmd), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ReportRequestResult, AppError>(CreateRequestResult(traineeId, templateId)));
        var controller = CreateController(service);

        var result = await controller.CreateReportRequest(traineeId.ToString(), new CreateReportRequestRequest
        {
            TemplateId = templateId.ToString(),
            Note = "note"
        });

        ((ObjectResult)result).StatusCode.Should().Be(StatusCodes.Status201Created);
        capturedTraineeId.Should().Be(traineeId);
        capturedCommand!.TemplateId.Should().Be(templateId);
    }

    [Test]
    public async Task GetTraineeSubmissions_WithInvalidTraineeId_ReturnsBadRequest()
    {
        var controller = CreateController(Substitute.For<IReportingService>());

        var result = await controller.GetTraineeSubmissions("bad-user");

        result.Should().BeAssignableTo<ObjectResult>();
        ((ObjectResult)result).StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Test]
    public async Task UpdateSubmissionFeedback_WithValidRequest_NormalizesFieldCommentsAndReturnsOk()
    {
        var service = Substitute.For<IReportingService>();
        var traineeId = Id<User>.New();
        var submissionId = Id<ReportSubmission>.New();
        UpdateReportSubmissionFeedbackCommand? captured = null;
        service.UpdateTrainerFeedbackAsync(Arg.Any<User>(), traineeId, submissionId, Arg.Do<UpdateReportSubmissionFeedbackCommand>(cmd => captured = cmd), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ReportSubmissionResult, AppError>(CreateSubmissionResult(traineeId)));
        var controller = CreateController(service);

        var result = await controller.UpdateSubmissionFeedback(traineeId.ToString(), submissionId.ToString(), new UpdateReportSubmissionFeedbackRequest
        {
            TrainerOverallComment = "overall",
            TrainerFieldComments = new Dictionary<string, string?> { ["Weight"] = "keep going" }
        });

        result.Should().BeOfType<OkObjectResult>();
        captured.Should().NotBeNull();
        captured!.FieldComments.Should().ContainKey("Weight");
    }

    private static TrainerReportingController CreateController(IReportingService reportingService)
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(typeof(Program).Assembly, typeof(IMappingProfile).Assembly);
        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var controller = new TrainerReportingController(reportingService, mapper)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.Items["User"] = new User { Id = Id<User>.New(), Name = "Trainer", Email = "trainer@example.com", ProfileRank = "Rookie" };
        return controller;
    }

    private static ReportTemplateResult CreateTemplateResult(Id<ReportTemplate>? templateId = null)
        => new()
        {
            Id = templateId ?? Id<ReportTemplate>.New(),
            TrainerId = Id<User>.New(),
            Name = "Weekly",
            CreatedAt = DateTimeOffset.UtcNow,
            Fields = [new ReportTemplateFieldResult { Key = "weight", Label = "Weight", Type = ReportFieldType.Number, Order = 1 }]
        };

    private static ReportRequestResult CreateRequestResult(Id<User> traineeId, Id<ReportTemplate> templateId)
        => new()
        {
            Id = Id<ReportRequest>.New(),
            TrainerId = Id<User>.New(),
            TraineeId = traineeId,
            TemplateId = templateId,
            Status = ReportRequestStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            Template = CreateTemplateResult(templateId)
        };

    private static ReportSubmissionResult CreateSubmissionResult(Id<User> traineeId)
        => new()
        {
            Id = Id<ReportSubmission>.New(),
            ReportRequestId = Id<ReportRequest>.New(),
            TraineeId = traineeId,
            SubmittedAt = DateTimeOffset.UtcNow,
            Request = CreateRequestResult(traineeId, Id<ReportTemplate>.New())
        };
}
