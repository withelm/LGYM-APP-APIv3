using LgymApi.Api;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Features.Trainer.Controllers;
using LgymApi.Api.Middleware;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Reporting.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Reporting.ApiAdapters;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
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

        Assert.That(result, Is.InstanceOf<ObjectResult>());
        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task CreateTemplate_WithValidRequest_ForwardsMappedFieldsAndReturnsCreated()
    {
        var service = Substitute.For<IReportingService>();
        CreateReportTemplateCommand? captured = null;
        var templateResult = CreateTemplateResult();
        service.CreateTemplateAsync(Arg.Any<AuthenticatedAccountContext>(), Arg.Do<CreateReportTemplateCommand>(cmd => captured = cmd), Arg.Any<CancellationToken>())
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

        Assert.That(result, Is.TypeOf<ObjectResult>());
        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(StatusCodes.Status201Created));
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Fields, Has.Exactly(1).Items);
        Assert.That(captured.Fields[0].Key, Is.EqualTo("weight"));
    }

    [Test]
    public async Task GetTemplates_ReturnsMappedDtos()
    {
        var service = Substitute.For<IReportingService>();
        service.GetTrainerTemplatesAsync(Arg.Any<AuthenticatedAccountContext>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<List<ReportTemplateResult>, AppError>([CreateTemplateResult()]));
        var controller = CreateController(service);

        var result = await controller.GetTemplates();

        var assertedValue162 = ((OkObjectResult)result).Value;
        Assert.That(assertedValue162, Is.InstanceOf<IEnumerable<ReportTemplateDto>>());
        var payload = ((IEnumerable<ReportTemplateDto>)assertedValue162);
        Assert.That(payload, Has.Exactly(1).Items);
    }

    [Test]
    public async Task UpdateTemplate_WithValidRequest_ForwardsParsedId()
    {
        var service = Substitute.For<IReportingService>();
        var templateId = Id<ReportTemplate>.New();
        Id<ReportTemplate> capturedId = Id<ReportTemplate>.Empty;
        service.UpdateTemplateAsync(Arg.Any<AuthenticatedAccountContext>(), Arg.Do<Id<ReportTemplate>>(id => capturedId = id), Arg.Any<CreateReportTemplateCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ReportTemplateResult, AppError>(CreateTemplateResult(templateId)));
        var controller = CreateController(service);

        var result = await controller.UpdateTemplate(templateId.ToString(), new UpsertReportTemplateRequest { Name = "Weekly", Fields = [] });

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        Assert.That(capturedId, Is.EqualTo(templateId));
    }

    [Test]
    public async Task DeleteTemplate_WithValidId_ReturnsDeletedMessage()
    {
        var service = Substitute.For<IReportingService>();
        service.DeleteTemplateAsync(Arg.Any<AuthenticatedAccountContext>(), Arg.Any<Id<ReportTemplate>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<Unit, AppError>(Unit.Value));
        var controller = CreateController(service);

        var result = await controller.DeleteTemplate(Id<ReportTemplate>.New().ToString());

        Assert.That(result, Is.TypeOf<OkObjectResult>());
    }

    [Test]
    public async Task CreateReportRequest_WithInvalidIds_ReturnsBadRequest()
    {
        var controller = CreateController(Substitute.For<IReportingService>());

        Assert.That((await controller.CreateReportRequest("bad-user", new CreateReportRequestRequest { TemplateId = Id<ReportTemplate>.New().ToString() })), Is.InstanceOf<ObjectResult>());
        Assert.That((await controller.CreateReportRequest(Id<User>.New().ToString(), new CreateReportRequestRequest { TemplateId = "bad-template" })), Is.InstanceOf<ObjectResult>());
    }

    [Test]
    public async Task CreateReportRequest_WithValidRequest_ForwardsParsedIds()
    {
        var service = Substitute.For<IReportingService>();
        var traineeId = Id<User>.New();
        var templateId = Id<ReportTemplate>.New();
        Id<AccountReference> capturedTraineeId = Id<AccountReference>.Empty;
        CreateReportRequestCommand? capturedCommand = null;
        service.CreateReportRequestAsync(Arg.Any<AuthenticatedAccountContext>(), Arg.Do<Id<AccountReference>>(id => capturedTraineeId = id), Arg.Do<CreateReportRequestCommand>(cmd => capturedCommand = cmd), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ReportRequestResult, AppError>(CreateRequestResult(traineeId, templateId)));
        var controller = CreateController(service);

        var result = await controller.CreateReportRequest(traineeId.ToString(), new CreateReportRequestRequest
        {
            TemplateId = templateId.ToString(),
            Note = "note"
        });

        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(StatusCodes.Status201Created));
        Assert.That(capturedTraineeId, Is.EqualTo(traineeId.Rebind<AccountReference>()));
        Assert.That(capturedCommand!.TemplateId, Is.EqualTo(templateId));
    }

    [Test]
    public async Task GetTraineeSubmissions_WithInvalidTraineeId_ReturnsBadRequest()
    {
        var controller = CreateController(Substitute.For<IReportingService>());

        var result = await controller.GetTraineeSubmissions("bad-user");

        Assert.That(result, Is.InstanceOf<ObjectResult>());
        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task UpdateSubmissionFeedback_WithValidRequest_NormalizesFieldCommentsAndReturnsOk()
    {
        var service = Substitute.For<IReportingService>();
        var traineeId = Id<User>.New();
        var submissionId = Id<ReportSubmission>.New();
        UpdateReportSubmissionFeedbackCommand? captured = null;
        service.UpdateTrainerFeedbackAsync(Arg.Any<AuthenticatedAccountContext>(), traineeId.Rebind<AccountReference>(), submissionId, Arg.Do<UpdateReportSubmissionFeedbackCommand>(cmd => captured = cmd), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ReportSubmissionResult, AppError>(CreateSubmissionResult(traineeId)));
        var controller = CreateController(service);

        var result = await controller.UpdateSubmissionFeedback(traineeId.ToString(), submissionId.ToString(), new UpdateReportSubmissionFeedbackRequest
        {
            TrainerOverallComment = "overall",
            TrainerFieldComments = new Dictionary<string, string?> { ["Weight"] = "keep going" }
        });

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.FieldComments, Does.ContainKey("Weight"));
    }

    [Test]
    public async Task UpdateSubmissionFeedback_WhenTrainerFieldCommentsIsNull_UsesEmptyDictionary()
    {
        var service = Substitute.For<IReportingService>();
        var traineeId = Id<User>.New();
        var submissionId = Id<ReportSubmission>.New();
        UpdateReportSubmissionFeedbackCommand? captured = null;
        service.UpdateTrainerFeedbackAsync(Arg.Any<AuthenticatedAccountContext>(), traineeId.Rebind<AccountReference>(), submissionId, Arg.Do<UpdateReportSubmissionFeedbackCommand>(cmd => captured = cmd), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ReportSubmissionResult, AppError>(CreateSubmissionResult(traineeId)));
        var controller = CreateController(service);

        var request = new UpdateReportSubmissionFeedbackRequest
        {
            TrainerOverallComment = "overall",
            TrainerFieldComments = null!
        };

        var result = await controller.UpdateSubmissionFeedback(traineeId.ToString(), submissionId.ToString(), request);

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.FieldComments, Is.Empty);
    }

    [Test]
    public async Task CreateRecurringReportAssignment_WithInvalidIds_ReturnsBadRequest()
    {
        var controller = CreateController(Substitute.For<IReportingService>());

        Assert.That((await controller.CreateRecurringReportAssignment("bad-user", new UpsertRecurringReportAssignmentRequest { TemplateId = Id<ReportTemplate>.New().ToString() })), Is.InstanceOf<ObjectResult>());
        Assert.That((await controller.CreateRecurringReportAssignment(Id<User>.New().ToString(), new UpsertRecurringReportAssignmentRequest { TemplateId = "bad-template" })), Is.InstanceOf<ObjectResult>());
    }

    [Test]
    public async Task CreateRecurringReportAssignment_WithValidRequest_ForwardsParsedValues()
    {
        var reportingService = Substitute.For<IReportingService>();
        var recurringService = Substitute.For<IRecurringReportAssignmentService>();
        var traineeId = Id<User>.New();
        var templateId = Id<ReportTemplate>.New();
        UpsertRecurringReportAssignmentCommand? captured = null;
        recurringService.CreateAsync(Arg.Any<AuthenticatedAccountContext>(), traineeId.Rebind<AccountReference>(), Arg.Do<UpsertRecurringReportAssignmentCommand>(cmd => captured = cmd), Arg.Any<CancellationToken>())
            .Returns(Result.Success<RecurringReportAssignmentResult, AppError>(CreateAssignmentResult(traineeId, templateId)));
        var controller = CreateController(reportingService, recurringService);
        var startsAt = DateTimeOffset.UtcNow;

        var result = await controller.CreateRecurringReportAssignment(traineeId.ToString(), new UpsertRecurringReportAssignmentRequest
        {
            TemplateId = templateId.ToString(),
            IntervalValue = 2,
            IntervalUnit = RecurringReportIntervalUnit.Month,
            StartsAt = startsAt,
            EndsAt = startsAt.AddDays(30),
            Note = "note"
        });

        Assert.That(result, Is.TypeOf<ObjectResult>());
        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(StatusCodes.Status201Created));
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.TemplateId, Is.EqualTo(templateId));
        Assert.That(captured.IntervalValue, Is.EqualTo(2));
        Assert.That(captured.IntervalUnit, Is.EqualTo(RecurringReportIntervalUnit.Month));
        Assert.That(captured.Note, Is.EqualTo("note"));
    }

    [Test]
    public async Task CreateRecurringReportAssignment_WhenServiceFails_ReturnsMappedErrorResult()
    {
        var recurringService = Substitute.For<IRecurringReportAssignmentService>();
        var traineeId = Id<User>.New();
        recurringService.CreateAsync(Arg.Any<AuthenticatedAccountContext>(), traineeId.Rebind<AccountReference>(), Arg.Any<UpsertRecurringReportAssignmentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<RecurringReportAssignmentResult, AppError>(new InvalidReportingError("bad request")));
        var controller = CreateController(Substitute.For<IReportingService>(), recurringService);

        var result = await controller.CreateRecurringReportAssignment(traineeId.ToString(), new UpsertRecurringReportAssignmentRequest
        {
            TemplateId = Id<ReportTemplate>.New().ToString(),
            IntervalValue = 1,
            IntervalUnit = RecurringReportIntervalUnit.Week,
            StartsAt = DateTimeOffset.UtcNow
        });

        Assert.That(result, Is.InstanceOf<ObjectResult>());
    }

    [Test]
    public async Task RequestRecurringReportNow_ForwardsAndReturnsCreated()
    {
        var recurringService = Substitute.For<IRecurringReportAssignmentService>();
        var traineeId = Id<User>.New();
        var assignmentId = Id<RecurringReportAssignment>.New();
        var templateId = Id<ReportTemplate>.New();
        var assignment = CreateAssignmentResult(traineeId, templateId);
        var currentRequest = CreateRequestResult(traineeId, templateId);
        assignment.Id = assignmentId;
        assignment.CurrentReportRequestId = currentRequest.Id;
        assignment.CurrentReportRequest = currentRequest;
        assignment.NextEligibleAt = null;
        using var cancellationSource = new CancellationTokenSource();
        recurringService.RequestNowAsync(
                Arg.Any<AuthenticatedAccountContext>(),
                traineeId.Rebind<AccountReference>(),
                assignmentId,
                cancellationSource.Token)
            .Returns(Result.Success<RecurringReportAssignmentResult, AppError>(assignment));
        var controller = CreateController(Substitute.For<IReportingService>(), recurringService);

        var result = await controller.RequestRecurringReportNow(
            traineeId.ToString(),
            assignmentId.ToString(),
            cancellationSource.Token);

        var assertedValue163 = result;
        Assert.That(assertedValue163, Is.TypeOf<ObjectResult>());
        var created = ((ObjectResult)assertedValue163);
        Assert.That(created.StatusCode, Is.EqualTo(StatusCodes.Status201Created));
        var assertedValue164 = created.Value;
        Assert.That(assertedValue164, Is.TypeOf<RecurringReportAssignmentDto>());
        var payload = ((RecurringReportAssignmentDto)assertedValue164);
        Assert.That(payload.Id, Is.EqualTo(assignmentId.ToString()));
        Assert.That(payload.CurrentReportRequestId, Is.EqualTo(currentRequest.Id.ToString()));
        Assert.That(payload.CurrentReportRequest, Is.Not.Null);
        Assert.That(payload.CurrentReportRequest!.Status, Is.EqualTo(ReportRequestStatus.Pending));
        Assert.That(payload.NextEligibleAt, Is.Null);
        await recurringService.Received(1).RequestNowAsync(
            Arg.Any<AuthenticatedAccountContext>(),
            traineeId.Rebind<AccountReference>(),
            assignmentId,
            cancellationSource.Token);
    }

    [Test]
    public async Task RequestRecurringReportNow_WithInvalidIds_ReturnsBadRequestWithoutForwarding()
    {
        var recurringService = Substitute.For<IRecurringReportAssignmentService>();
        var controller = CreateController(Substitute.For<IReportingService>(), recurringService);

        var invalidTrainee = await controller.RequestRecurringReportNow(
            "not-a-guid",
            Id<RecurringReportAssignment>.New().ToString());
        var invalidAssignment = await controller.RequestRecurringReportNow(
            Id<User>.New().ToString(),
            "not-a-guid");

        Assert.That(((ObjectResult)invalidTrainee).StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        Assert.That(((ObjectResult)invalidAssignment).StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        await recurringService.DidNotReceive().RequestNowAsync(
            Arg.Any<AuthenticatedAccountContext>(),
            Arg.Any<Id<AccountReference>>(),
            Arg.Any<Id<RecurringReportAssignment>>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RequestRecurringReportNow_WhenPortFails_ReturnsMappedError()
    {
        var recurringService = Substitute.For<IRecurringReportAssignmentService>();
        var traineeId = Id<User>.New();
        var assignmentId = Id<RecurringReportAssignment>.New();
        recurringService.RequestNowAsync(
                Arg.Any<AuthenticatedAccountContext>(),
                traineeId.Rebind<AccountReference>(),
                assignmentId,
                Arg.Any<CancellationToken>())
            .Returns(Result.Failure<RecurringReportAssignmentResult, AppError>(
                new ReportingConflictError("conflict")));
        var controller = CreateController(Substitute.For<IReportingService>(), recurringService);

        var result = await controller.RequestRecurringReportNow(traineeId.ToString(), assignmentId.ToString());

        var assertedValue165 = result;
        Assert.That(assertedValue165, Is.TypeOf<ObjectResult>());
        var conflict = ((ObjectResult)assertedValue165);
        Assert.That(conflict.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        Assert.That(conflict.Value, Is.EqualTo(new ResponseMessageDto { Message = "conflict" }).UsingPropertiesComparer());
    }

    [Test]
    public async Task GetRecurringReportAssignments_WithInvalidTraineeId_ReturnsBadRequest()
    {
        var controller = CreateController(Substitute.For<IReportingService>());

        var result = await controller.GetRecurringReportAssignments("bad-user");

        Assert.That(result, Is.InstanceOf<ObjectResult>());
        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetRecurringReportAssignments_WhenServiceSucceeds_ReturnsMappedDtos()
    {
        var reportingService = Substitute.For<IReportingService>();
        var recurringService = Substitute.For<IRecurringReportAssignmentService>();
        var traineeId = Id<User>.New();
        recurringService.GetForTraineeAsync(Arg.Any<AuthenticatedAccountContext>(), traineeId.Rebind<AccountReference>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<List<RecurringReportAssignmentResult>, AppError>([CreateAssignmentResult(traineeId, Id<ReportTemplate>.New())]));
        var controller = CreateController(reportingService, recurringService);

        var result = await controller.GetRecurringReportAssignments(traineeId.ToString());

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        Assert.That(((OkObjectResult)result).Value, Is.InstanceOf<IEnumerable<RecurringReportAssignmentDto>>());
    }

    [Test]
    public async Task GetRecurringReportAssignments_WhenServiceFails_ReturnsMappedErrorResult()
    {
        var recurringService = Substitute.For<IRecurringReportAssignmentService>();
        var traineeId = Id<User>.New();
        recurringService.GetForTraineeAsync(Arg.Any<AuthenticatedAccountContext>(), traineeId.Rebind<AccountReference>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<List<RecurringReportAssignmentResult>, AppError>(new InvalidReportingError("bad request")));
        var controller = CreateController(Substitute.For<IReportingService>(), recurringService);

        var result = await controller.GetRecurringReportAssignments(traineeId.ToString());

        Assert.That(result, Is.InstanceOf<ObjectResult>());
    }

    [Test]
    public async Task UpdateRecurringReportAssignment_WithInvalidIds_ReturnsBadRequest()
    {
        var controller = CreateController(Substitute.For<IReportingService>());

        Assert.That((await controller.UpdateRecurringReportAssignment("bad-user", Id<RecurringReportAssignment>.New().ToString(), new UpsertRecurringReportAssignmentRequest { TemplateId = Id<ReportTemplate>.New().ToString() })), Is.InstanceOf<ObjectResult>());
        Assert.That((await controller.UpdateRecurringReportAssignment(Id<User>.New().ToString(), "bad-assignment", new UpsertRecurringReportAssignmentRequest { TemplateId = Id<ReportTemplate>.New().ToString() })), Is.InstanceOf<ObjectResult>());
    }

    [Test]
    public async Task UpdateRecurringReportAssignment_WithValidIds_ForwardsParsedValues()
    {
        var reportingService = Substitute.For<IReportingService>();
        var recurringService = Substitute.For<IRecurringReportAssignmentService>();
        var traineeId = Id<User>.New();
        var assignmentId = Id<RecurringReportAssignment>.New();
        var templateId = Id<ReportTemplate>.New();
        Id<RecurringReportAssignment> capturedAssignmentId = Id<RecurringReportAssignment>.Empty;
        recurringService.UpdateAsync(Arg.Any<AuthenticatedAccountContext>(), traineeId.Rebind<AccountReference>(), Arg.Do<Id<RecurringReportAssignment>>(id => capturedAssignmentId = id), Arg.Any<UpsertRecurringReportAssignmentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<RecurringReportAssignmentResult, AppError>(CreateAssignmentResult(traineeId, templateId)));
        var controller = CreateController(reportingService, recurringService);

        var result = await controller.UpdateRecurringReportAssignment(traineeId.ToString(), assignmentId.ToString(), new UpsertRecurringReportAssignmentRequest
        {
            TemplateId = templateId.ToString(),
            IntervalValue = 1,
            IntervalUnit = RecurringReportIntervalUnit.Week,
            StartsAt = DateTimeOffset.UtcNow
        });

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        Assert.That(capturedAssignmentId, Is.EqualTo(assignmentId));
    }

    [Test]
    public async Task UpdateRecurringReportAssignment_WhenServiceFails_ReturnsMappedErrorResult()
    {
        var recurringService = Substitute.For<IRecurringReportAssignmentService>();
        var traineeId = Id<User>.New();
        var assignmentId = Id<RecurringReportAssignment>.New();
        recurringService.UpdateAsync(Arg.Any<AuthenticatedAccountContext>(), traineeId.Rebind<AccountReference>(), assignmentId, Arg.Any<UpsertRecurringReportAssignmentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<RecurringReportAssignmentResult, AppError>(new InvalidReportingError("bad request")));
        var controller = CreateController(Substitute.For<IReportingService>(), recurringService);

        var result = await controller.UpdateRecurringReportAssignment(traineeId.ToString(), assignmentId.ToString(), new UpsertRecurringReportAssignmentRequest
        {
            TemplateId = Id<ReportTemplate>.New().ToString(),
            IntervalValue = 1,
            IntervalUnit = RecurringReportIntervalUnit.Week,
            StartsAt = DateTimeOffset.UtcNow
        });

        Assert.That(result, Is.InstanceOf<ObjectResult>());
    }

    [Test]
    public async Task PauseRecurringReportAssignment_WithInvalidIds_ReturnsBadRequest()
    {
        var controller = CreateController(Substitute.For<IReportingService>());

        Assert.That((await controller.PauseRecurringReportAssignment("bad-user", Id<RecurringReportAssignment>.New().ToString())), Is.InstanceOf<ObjectResult>());
        Assert.That((await controller.PauseRecurringReportAssignment(Id<User>.New().ToString(), "bad-assignment")), Is.InstanceOf<ObjectResult>());
    }

    [Test]
    public async Task PauseRecurringReportAssignment_WithValidIds_ForwardsToService()
    {
        var reportingService = Substitute.For<IReportingService>();
        var recurringService = Substitute.For<IRecurringReportAssignmentService>();
        var traineeId = Id<User>.New();
        var assignmentId = Id<RecurringReportAssignment>.New();
        recurringService.PauseAsync(Arg.Any<AuthenticatedAccountContext>(), traineeId.Rebind<AccountReference>(), assignmentId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<RecurringReportAssignmentResult, AppError>(CreateAssignmentResult(traineeId, Id<ReportTemplate>.New())));
        var controller = CreateController(reportingService, recurringService);

        var result = await controller.PauseRecurringReportAssignment(traineeId.ToString(), assignmentId.ToString());

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        await recurringService.Received(1).PauseAsync(Arg.Any<AuthenticatedAccountContext>(), traineeId.Rebind<AccountReference>(), assignmentId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PauseRecurringReportAssignment_WhenServiceFails_ReturnsMappedErrorResult()
    {
        var recurringService = Substitute.For<IRecurringReportAssignmentService>();
        var traineeId = Id<User>.New();
        var assignmentId = Id<RecurringReportAssignment>.New();
        recurringService.PauseAsync(Arg.Any<AuthenticatedAccountContext>(), traineeId.Rebind<AccountReference>(), assignmentId, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<RecurringReportAssignmentResult, AppError>(new InvalidReportingError("bad request")));
        var controller = CreateController(Substitute.For<IReportingService>(), recurringService);

        var result = await controller.PauseRecurringReportAssignment(traineeId.ToString(), assignmentId.ToString());

        Assert.That(result, Is.InstanceOf<ObjectResult>());
    }

    [Test]
    public async Task ResumeRecurringReportAssignment_WithInvalidIds_ReturnsBadRequest()
    {
        var controller = CreateController(Substitute.For<IReportingService>());

        Assert.That((await controller.ResumeRecurringReportAssignment("bad-user", Id<RecurringReportAssignment>.New().ToString())), Is.InstanceOf<ObjectResult>());
        Assert.That((await controller.ResumeRecurringReportAssignment(Id<User>.New().ToString(), "bad-assignment")), Is.InstanceOf<ObjectResult>());
    }

    [Test]
    public async Task ResumeRecurringReportAssignment_WithValidIds_ForwardsToService()
    {
        var reportingService = Substitute.For<IReportingService>();
        var recurringService = Substitute.For<IRecurringReportAssignmentService>();
        var traineeId = Id<User>.New();
        var assignmentId = Id<RecurringReportAssignment>.New();
        recurringService.ResumeAsync(Arg.Any<AuthenticatedAccountContext>(), traineeId.Rebind<AccountReference>(), assignmentId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<RecurringReportAssignmentResult, AppError>(CreateAssignmentResult(traineeId, Id<ReportTemplate>.New())));
        var controller = CreateController(reportingService, recurringService);

        var result = await controller.ResumeRecurringReportAssignment(traineeId.ToString(), assignmentId.ToString());

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        await recurringService.Received(1).ResumeAsync(Arg.Any<AuthenticatedAccountContext>(), traineeId.Rebind<AccountReference>(), assignmentId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ResumeRecurringReportAssignment_WhenServiceFails_ReturnsMappedErrorResult()
    {
        var recurringService = Substitute.For<IRecurringReportAssignmentService>();
        var traineeId = Id<User>.New();
        var assignmentId = Id<RecurringReportAssignment>.New();
        recurringService.ResumeAsync(Arg.Any<AuthenticatedAccountContext>(), traineeId.Rebind<AccountReference>(), assignmentId, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<RecurringReportAssignmentResult, AppError>(new InvalidReportingError("bad request")));
        var controller = CreateController(Substitute.For<IReportingService>(), recurringService);

        var result = await controller.ResumeRecurringReportAssignment(traineeId.ToString(), assignmentId.ToString());

        Assert.That(result, Is.InstanceOf<ObjectResult>());
    }

    [Test]
    public async Task DeleteRecurringReportAssignment_WithInvalidIds_ReturnsBadRequest()
    {
        var controller = CreateController(Substitute.For<IReportingService>());

        Assert.That((await controller.DeleteRecurringReportAssignment("bad-user", Id<RecurringReportAssignment>.New().ToString())), Is.InstanceOf<ObjectResult>());
        Assert.That((await controller.DeleteRecurringReportAssignment(Id<User>.New().ToString(), "bad-assignment")), Is.InstanceOf<ObjectResult>());
    }

    [Test]
    public async Task DeleteRecurringReportAssignment_WithValidIds_ForwardsToService()
    {
        var reportingService = Substitute.For<IReportingService>();
        var recurringService = Substitute.For<IRecurringReportAssignmentService>();
        var traineeId = Id<User>.New();
        var assignmentId = Id<RecurringReportAssignment>.New();
        recurringService.DeleteAsync(Arg.Any<AuthenticatedAccountContext>(), traineeId.Rebind<AccountReference>(), assignmentId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Unit, AppError>(Unit.Value));
        var controller = CreateController(reportingService, recurringService);

        var result = await controller.DeleteRecurringReportAssignment(traineeId.ToString(), assignmentId.ToString());

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        await recurringService.Received(1).DeleteAsync(Arg.Any<AuthenticatedAccountContext>(), traineeId.Rebind<AccountReference>(), assignmentId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteRecurringReportAssignment_WhenServiceFails_ReturnsMappedErrorResult()
    {
        var recurringService = Substitute.For<IRecurringReportAssignmentService>();
        var traineeId = Id<User>.New();
        var assignmentId = Id<RecurringReportAssignment>.New();
        recurringService.DeleteAsync(Arg.Any<AuthenticatedAccountContext>(), traineeId.Rebind<AccountReference>(), assignmentId, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<Unit, AppError>(new InvalidReportingError("bad request")));
        var controller = CreateController(Substitute.For<IReportingService>(), recurringService);

        var result = await controller.DeleteRecurringReportAssignment(traineeId.ToString(), assignmentId.ToString());

        Assert.That(result, Is.InstanceOf<ObjectResult>());
    }

    private static TrainerReportingController CreateController(IReportingService reportingService, IRecurringReportAssignmentService? recurringService = null)
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);
        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var controller = new TrainerReportingController(new TrainerReportTemplateApiAdapter(reportingService), new TrainerReportRequestApiAdapter(reportingService), new TrainerReportPhotoApiAdapter(reportingService, mapper), new RecurringReportAssignmentApiAdapter(recurringService ?? Substitute.For<IRecurringReportAssignmentService>()), mapper)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.Features.Set<IAuthenticatedAccountContextFeature>(new AuthenticatedAccountContextFeature(
            new AuthenticatedAccountContext(Id<AccountReference>.New(), null, [], [], false, false)));
        return controller;
    }

    private static ReportTemplateResult CreateTemplateResult(Id<ReportTemplate>? templateId = null)
        => new()
        {
            Id = templateId ?? Id<ReportTemplate>.New(),
            TrainerId = Id<AccountReference>.New(),
            Name = "Weekly",
            CreatedAt = DateTimeOffset.UtcNow,
            Fields = [new ReportTemplateFieldResult { Key = "weight", Label = "Weight", Type = ReportFieldType.Number, Order = 1 }]
        };

    private static ReportRequestResult CreateRequestResult(Id<User> traineeId, Id<ReportTemplate> templateId)
        => new()
        {
            Id = Id<ReportRequest>.New(),
            TrainerId = Id<AccountReference>.New(),
            TraineeId = traineeId.Rebind<AccountReference>(),
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
            TraineeId = traineeId.Rebind<AccountReference>(),
            SubmittedAt = DateTimeOffset.UtcNow,
            Request = CreateRequestResult(traineeId, Id<ReportTemplate>.New())
        };

    private static RecurringReportAssignmentResult CreateAssignmentResult(Id<User> traineeId, Id<ReportTemplate> templateId)
        => new()
        {
            Id = Id<RecurringReportAssignment>.New(),
            TrainerId = Id<AccountReference>.New(),
            TraineeId = traineeId.Rebind<AccountReference>(),
            TemplateId = templateId,
            IntervalValue = 1,
            IntervalUnit = RecurringReportIntervalUnit.Week,
            StartsAt = DateTimeOffset.UtcNow,
            Template = CreateTemplateResult(templateId)
        };
}
