using LgymApi.Api;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Features.Trainer.Controllers;
using LgymApi.Api.Middleware;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Reporting.ApiAdapters;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TrainerReportingPhotoControllerTests
{
    [Test]
    public async Task InitiatePhotoUpload_WithInvalidReportRequestId_ReturnsBadRequest()
    {
        var controller = CreateController(Substitute.For<IReportingService>());

        var result = await controller.InitiatePhotoUpload(new InitiatePhotoUploadRequest
        {
            ReportRequestId = "not-a-guid",
            ViewType = "Front",
            MimeType = "image/jpeg",
            SizeBytes = 123
        });

        Assert.That(result, Is.InstanceOf<ObjectResult>());
        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task InitiatePhotoUpload_WithValidRequest_ForwardsParsedCommandAndReturnsOk()
    {
        var reportingService = Substitute.For<IReportingService>();
        InitiatePhotoUploadCommand? capturedCommand = null;
        var requestId = Id<ReportRequest>.New();

        reportingService
            .InitiatePhotoUploadAsync(Arg.Any<AuthenticatedAccountContext>(), Arg.Do<InitiatePhotoUploadCommand>(command => capturedCommand = command), Arg.Any<CancellationToken>())
            .Returns(Result.Success<InitiatePhotoUploadResult, AppError>(new InitiatePhotoUploadResult
            {
                UploadUrl = "https://upload.example.com",
                StorageKey = "photos/test/front.jpg",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
            }));

        var controller = CreateController(reportingService);

        var result = await controller.InitiatePhotoUpload(new InitiatePhotoUploadRequest
        {
            ReportRequestId = requestId.ToString(),
            ViewType = "Front",
            MimeType = "image/jpeg",
            SizeBytes = 456
        });

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        Assert.That(capturedCommand, Is.Not.Null);
        Assert.That(capturedCommand!.ReportRequestId, Is.EqualTo(requestId));
        Assert.That(capturedCommand.ViewType, Is.EqualTo("Front"));
        Assert.That(capturedCommand.MimeType, Is.EqualTo("image/jpeg"));
        Assert.That(capturedCommand.SizeBytes, Is.EqualTo(456));
        Assert.That(((OkObjectResult)result).Value, Is.TypeOf<InitiatePhotoUploadResponse>());
    }

    [Test]
    public async Task GetPhotoSignedReadUrl_ForwardsPhotoIdAndReturnsOk()
    {
        var reportingService = Substitute.For<IReportingService>();
        var photoId = Id<Photo>.New();

        reportingService
            .GetSignedReadUrlAsync(Arg.Any<AuthenticatedAccountContext>(), photoId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<SignedReadUrlResult, AppError>(new SignedReadUrlResult
            {
                ReadUrl = "https://read.example.com",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
            }));

        var controller = CreateController(reportingService);

        var result = await controller.GetPhotoSignedReadUrl(photoId.ToString());

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var assertedValue166 = ((OkObjectResult)result).Value;
        Assert.That(assertedValue166, Is.TypeOf<GetSignedReadUrlResponse>());
        var response = ((GetSignedReadUrlResponse)assertedValue166);
        Assert.That(response.ReadUrl, Is.EqualTo("https://read.example.com"));
        await reportingService.Received(1).GetSignedReadUrlAsync(Arg.Any<AuthenticatedAccountContext>(), photoId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetPhotoSignedReadUrl_WithMalformedPhotoId_ReturnsExistingBadRequestPayload()
    {
        var reportingService = Substitute.For<IReportingService>();
        var controller = CreateController(reportingService);

        var result = await controller.GetPhotoSignedReadUrl("not-a-guid");

        var assertedValue167 = result;
        Assert.That(assertedValue167, Is.TypeOf<BadRequestObjectResult>());
        var objectResult = ((BadRequestObjectResult)assertedValue167);
        Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var assertedValue168 = objectResult.Value;
        Assert.That(assertedValue168, Is.TypeOf<ResponseMessageDto>());
        Assert.That(((ResponseMessageDto)assertedValue168).Message, Is.EqualTo("Invalid photo ID format"));
        await reportingService.DidNotReceive().GetSignedReadUrlAsync(Arg.Any<AuthenticatedAccountContext>(), Arg.Any<Id<Photo>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetPhotoSignedReadUrl_WithEmptyPhotoId_ReturnsResourceBackedBadRequestPayload()
    {
        var reportingService = Substitute.For<IReportingService>();
        var controller = CreateController(reportingService);

        var result = await controller.GetPhotoSignedReadUrl(string.Empty);

        var assertedValue169 = result;
        Assert.That(assertedValue169, Is.TypeOf<BadRequestObjectResult>());
        var objectResult = ((BadRequestObjectResult)assertedValue169);
        Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var assertedValue170 = objectResult.Value;
        Assert.That(assertedValue170, Is.TypeOf<ResponseMessageDto>());
        Assert.That(((ResponseMessageDto)assertedValue170).Message, Is.EqualTo(Messages.FieldRequired));
        await reportingService.DidNotReceive().GetSignedReadUrlAsync(Arg.Any<AuthenticatedAccountContext>(), Arg.Any<Id<Photo>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CompletePhotoUpload_WithInvalidReportRequestId_ReturnsBadRequest()
    {
        var controller = CreateController(Substitute.For<IReportingService>());

        var result = await controller.CompletePhotoUpload(new CompletePhotoUploadRequest
        {
            ReportRequestId = "bad-id",
            StorageKey = "photos/test/front.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 100,
            Checksum = "etag",
            ViewType = "Front"
        });

        Assert.That(result, Is.InstanceOf<ObjectResult>());
        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task CompletePhotoUpload_WithValidRequest_ForwardsParsedCommandAndReturnsOk()
    {
        var reportingService = Substitute.For<IReportingService>();
        CompletePhotoUploadCommand? capturedCommand = null;
        var requestId = Id<ReportRequest>.New();
        var photoId = Id<Photo>.New();

        reportingService
            .CompletePhotoUploadAsync(Arg.Any<AuthenticatedAccountContext>(), Arg.Do<CompletePhotoUploadCommand>(command => capturedCommand = command), Arg.Any<CancellationToken>())
            .Returns(Result.Success<CompletePhotoUploadResult, AppError>(new CompletePhotoUploadResult
            {
                PhotoId = photoId,
                UploadedAt = DateTimeOffset.UtcNow
            }));

        var controller = CreateController(reportingService);

        var result = await controller.CompletePhotoUpload(new CompletePhotoUploadRequest
        {
            ReportRequestId = requestId.ToString(),
            StorageKey = "photos/test/front.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 100,
            Checksum = "etag",
            ViewType = "Front"
        });

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        Assert.That(capturedCommand, Is.Not.Null);
        Assert.That(capturedCommand!.ReportRequestId, Is.EqualTo(requestId));
        Assert.That(capturedCommand.StorageKey, Is.EqualTo("photos/test/front.jpg"));
        Assert.That(capturedCommand.Checksum, Is.EqualTo("etag"));
        Assert.That(((OkObjectResult)result).Value, Is.TypeOf<CompletePhotoUploadResponse>());
    }

    [Test]
    public async Task GetPhotoHistory_WithInvalidTraineeId_ReturnsBadRequest()
    {
        var controller = CreateController(Substitute.For<IReportingService>());

        var result = await controller.GetPhotoHistory("invalid-user", null);

        Assert.That(result, Is.InstanceOf<ObjectResult>());
        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetPhotoHistory_WithValidIds_ForwardsParsedIdsAndReturnsOk()
    {
        var reportingService = Substitute.For<IReportingService>();
        GetPhotoHistoryCommand? capturedCommand = null;
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var photoId = Id<Photo>.New();

        reportingService
            .GetPhotoHistoryAsync(Arg.Any<AuthenticatedAccountContext>(), Arg.Do<GetPhotoHistoryCommand>(command => capturedCommand = command), Arg.Any<CancellationToken>())
            .Returns(Result.Success<List<PhotoHistoryItemResult>, AppError>(
            [
                new PhotoHistoryItemResult
                {
                    Id = photoId,
                    ViewType = "Front",
                    SizeBytes = 2048,
                    ReadUrl = "https://read.example.com",
                    ReportRequestId = requestId,
                    UploadedAt = DateTimeOffset.UtcNow
                }
            ]));

        var controller = CreateController(reportingService);

        var result = await controller.GetPhotoHistory(traineeId.ToString(), requestId.ToString());

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        Assert.That(capturedCommand, Is.Not.Null);
        Assert.That(capturedCommand!.TraineeId, Is.EqualTo(traineeId.Rebind<AccountReference>()));
        Assert.That(capturedCommand.RequestId, Is.EqualTo(requestId));
        var assertedValue171 = ((OkObjectResult)result).Value;
        Assert.That(assertedValue171, Is.TypeOf<GetPhotoHistoryResponse>());
        var response = ((GetPhotoHistoryResponse)assertedValue171);
        Assert.That(response.Photos, Has.Exactly(1).Items);
        Assert.That(response.Photos[0].Id, Is.EqualTo(photoId.ToString()));
    }

    private static TrainerReportingController CreateController(IReportingService reportingService)
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);
        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var recurringService = Substitute.For<IRecurringReportAssignmentService>();

        var controller = new TrainerReportingController(new TrainerReportTemplateApiAdapter(reportingService), new TrainerReportRequestApiAdapter(reportingService), new TrainerReportPhotoApiAdapter(reportingService, mapper), new RecurringReportAssignmentApiAdapter(recurringService), mapper)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        controller.HttpContext.Features.Set<IAuthenticatedAccountContextFeature>(new AuthenticatedAccountContextFeature(
            new AuthenticatedAccountContext(Id<AccountReference>.New(), null, [], [], false, false)));

        return controller;
    }
}
