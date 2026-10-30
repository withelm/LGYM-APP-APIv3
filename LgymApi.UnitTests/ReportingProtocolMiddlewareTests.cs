using System.Text;
using System.Text.Json;
using FluentAssertions;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Idempotency;
using LgymApi.Api.Middleware;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ReportingProtocolMiddlewareTests
{
    [TestCase("")]
    [TestCase(" 2 ")]
    [TestCase("3")]
    [TestCase("02")]
    public async Task InvokeAsync_WithInvalidOrUnsupportedHeader_FailsClosed(string value)
    {
        var context = CreateContext(new ReportingProtocolAttribute());
        context.Request.Headers[ReportingProtocolMiddleware.HeaderName] = value;
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        nextCalled.Should().BeFalse();
        (await ReadErrorCodeAsync(context)).Should().Be(ReportingProtocolResponses.UnsupportedCode);
    }

    [Test]
    public async Task InvokeAsync_WithMissingHeaderAndChunkedSchemaTwoTemplate_UsesP1AndRejectsBody()
    {
        var context = CreateContext(new ReportingProtocolAttribute { RequestKind = ReportingProtocolRequestKind.Template });
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentLength = null;
        context.Request.Body = JsonBody("""
            { "fields": [{ "moduleConfig": { "schemaVersion": 2, "requiredPhotoTypeIds": ["id"] } }] }
            """);
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        nextCalled.Should().BeFalse();
        (await ReadErrorCodeAsync(context)).Should().Be(ReportingProtocolResponses.V2RequiredCode);
    }

    [Test]
    public async Task InvokeAsync_WithP1AnswerTextContainingProtocolPropertyNames_DoesNotScanAnswers()
    {
        var context = CreateContext(new ReportingProtocolAttribute());
        context.Request.Method = HttpMethods.Post;
        context.Request.Body = JsonBody("""
            { "answers": { "freeText": "requiredPhotoTypeIds", "moduleConfig": { "schemaVersion": 2 } } }
            """);
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Test]
    public async Task InvokeAsync_WithP1DictionaryRoute_ReturnsUpgradeRequiredBeforeHandler()
    {
        var context = CreateContext(new ReportingProtocolAttribute(2));
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status426UpgradeRequired);
        nextCalled.Should().BeFalse();
        (await ReadErrorCodeAsync(context)).Should().Be(ReportingProtocolResponses.V2RequiredCode);
    }

    [Test]
    public async Task InvokeAsync_WithEnabledP2_AcknowledgesAndPartitionsPrivateCache()
    {
        var context = CreateContext(new ReportingProtocolAttribute());
        context.Request.Headers[ReportingProtocolMiddleware.HeaderName] = "2";
        var middleware = CreateMiddleware(async nextContext =>
            await nextContext.Response.WriteAsJsonAsync(new { ok = true }));

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.Headers[ReportingProtocolMiddleware.HeaderName].ToString().Should().Be("2");
        context.Response.Headers.CacheControl.ToString().Should().Be("private, no-store");
        context.Response.Headers.Vary.ToString().Should().Contain(ReportingProtocolMiddleware.HeaderName);
    }

    [Test]
    public async Task InvokeAsync_WithP2UploadWithoutPhotoTypeId_RejectsBeforeController()
    {
        var context = CreateContext(new ReportingProtocolAttribute { RequestKind = ReportingProtocolRequestKind.Upload });
        context.Request.Method = HttpMethods.Post;
        context.Request.Headers[ReportingProtocolMiddleware.HeaderName] = "2";
        context.Request.Body = JsonBody("{ \"reportRequestId\": \"request\" }");
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        (await ReadErrorCodeAsync(context)).Should().Be(ReportingProtocolResponses.PhotoTypeRequiredCode);
    }

    [Test]
    public async Task InvokeAsync_WithP1UploadContainingProtocolTwoBinding_RejectsDowngrade()
    {
        var context = CreateContext(new ReportingProtocolAttribute { RequestKind = ReportingProtocolRequestKind.Upload });
        context.Request.Method = HttpMethods.Post;
        context.Request.Body = JsonBody("{ \"photoTypeId\": \"type\", \"reportPhotoSlotId\": \"slot\" }");
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        (await ReadErrorCodeAsync(context)).Should().Be(ReportingProtocolResponses.V2RequiredCode);
    }

    [Test]
    public void ResultFilter_WithBuiltInSchemaTwoSnapshot_AdaptsToLegacyRequiredViews()
    {
        var photoTypeId = LgymApi.Domain.ValueObjects.Id<TrainerPhotoType>.New().ToString();
        var template = CreateSnapshotTemplate(photoTypeId, PhotoTypeNameMode.SystemLocalized, "Front");

        var compatible = ReportingProtocolResultFilter.TryAdapt(template);

        compatible.Should().BeTrue();
        var config = template.Fields.Single().ModuleConfig!.Value;
        config.TryGetProperty("schemaVersion", out _).Should().BeFalse();
        config.GetProperty("requiredViews").EnumerateArray().Single().GetString().Should().Be("Front");
    }

    [TestCase(PhotoTypeNameMode.TrainerProvided, "Front")]
    [TestCase(PhotoTypeNameMode.SystemLocalized, null)]
    public void ResultFilter_WithCustomOrLiteralOverrideSnapshot_RequiresUpgrade(
        PhotoTypeNameMode nameMode,
        string? systemKey)
    {
        var photoTypeId = LgymApi.Domain.ValueObjects.Id<TrainerPhotoType>.New().ToString();
        var template = CreateSnapshotTemplate(photoTypeId, nameMode, systemKey);

        ReportingProtocolResultFilter.TryAdapt(template).Should().BeFalse();
    }

    [Test]
    public async Task IdempotencyScopeAndFingerprint_IsolateCanonicalReportingProtocol()
    {
        var p1 = await CaptureIdempotencyRecordAsync(1);
        var p1Again = await CaptureIdempotencyRecordAsync(1);
        var p2 = await CaptureIdempotencyRecordAsync(2);

        p1.ScopeTuple.Should().Be(p1Again.ScopeTuple);
        p1.RequestFingerprint.Should().Be(p1Again.RequestFingerprint);
        p1.ScopeTuple.Should().EndWith("|reporting-protocol:1");
        p2.ScopeTuple.Should().EndWith("|reporting-protocol:2");
        p2.ScopeTuple.Should().NotBe(p1.ScopeTuple);
        p2.RequestFingerprint.Should().NotBe(p1.RequestFingerprint);
    }

    private static ReportingProtocolMiddleware CreateMiddleware(RequestDelegate next) => new(next);

    private static DefaultHttpContext CreateContext(params object[] metadata)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), "reporting test"));
        return context;
    }

    private static MemoryStream JsonBody(string json) => new(Encoding.UTF8.GetBytes(json));

    private static async Task<string> ReadErrorCodeAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return document.RootElement.GetProperty("code").GetString()!;
    }

    private static ReportTemplateDto CreateSnapshotTemplate(
        string photoTypeId,
        PhotoTypeNameMode nameMode,
        string? systemKey) => new()
    {
        Fields =
        [
            new ReportTemplateFieldDto
            {
                Type = ReportFieldType.Photos,
                ModuleConfig = JsonSerializer.SerializeToElement(new
                {
                    schemaVersion = 2,
                    requiredPhotoTypeIds = new[] { photoTypeId },
                    photoTypes = new[]
                    {
                        new
                        {
                            photoTypeId,
                            reportPhotoSlotId = "slot",
                            systemKey,
                            nameMode
                        }
                    }
                })
            }
        ]
    };

    private static async Task<ApiIdempotencyRecord> CaptureIdempotencyRecordAsync(int protocol)
    {
        var context = CreateContext(new ApiIdempotencyAttribute(
            "/api/reporting-test",
            ApiIdempotencyScopeSource.AuthenticatedUser));
        context.Request.Method = HttpMethods.Post;
        context.Request.Headers[ApiIdempotencyHeaders.IdempotencyKey] = "same-key";
        context.Request.Body = JsonBody("{ \"value\": 1 }");
        ReportingProtocolContext.SetVersion(context, protocol);

        ApiIdempotencyRecord? captured = null;
        var repository = Substitute.For<IApiIdempotencyRecordRepository>();
        repository.AddOrGetExistingAsync(Arg.Any<ApiIdempotencyRecord>(), context.RequestAborted)
            .Returns(call =>
            {
                captured = call.Arg<ApiIdempotencyRecord>();
                return captured;
            });
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(context.RequestAborted).Returns(1);
        var middleware = new ApiIdempotencyMiddleware(async nextContext =>
            await nextContext.Response.WriteAsJsonAsync(new { ok = true }));

        await middleware.InvokeAsync(context, repository, unitOfWork);

        return captured!;
    }
}
