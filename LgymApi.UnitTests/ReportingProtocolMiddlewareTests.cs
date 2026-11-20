using System.Text;
using System.Text.Json;
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

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        Assert.That(nextCalled, Is.False);
        Assert.That((await ReadErrorCodeAsync(context)), Is.EqualTo(ReportingProtocolResponses.UnsupportedCode));
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

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        Assert.That(nextCalled, Is.False);
        Assert.That((await ReadErrorCodeAsync(context)), Is.EqualTo(ReportingProtocolResponses.V2RequiredCode));
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

        Assert.That(nextCalled, Is.True);
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
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

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status426UpgradeRequired));
        Assert.That(nextCalled, Is.False);
        Assert.That((await ReadErrorCodeAsync(context)), Is.EqualTo(ReportingProtocolResponses.V2RequiredCode));
    }

    [Test]
    public async Task InvokeAsync_WithEnabledP2_AcknowledgesAndPartitionsPrivateCache()
    {
        var context = CreateContext(new ReportingProtocolAttribute());
        context.Request.Headers[ReportingProtocolMiddleware.HeaderName] = "2";
        var middleware = CreateMiddleware(async nextContext =>
            await nextContext.Response.WriteAsJsonAsync(new { ok = true }));

        await middleware.InvokeAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
        Assert.That(context.Response.Headers[ReportingProtocolMiddleware.HeaderName].ToString(), Is.EqualTo("2"));
        Assert.That(context.Response.Headers.CacheControl.ToString(), Is.EqualTo("private, no-store"));
        Assert.That(context.Response.Headers.Vary.ToString(), Does.Contain(ReportingProtocolMiddleware.HeaderName));
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

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        Assert.That((await ReadErrorCodeAsync(context)), Is.EqualTo(ReportingProtocolResponses.PhotoTypeRequiredCode));
    }

    [Test]
    public async Task InvokeAsync_WithP1UploadContainingProtocolTwoBinding_RejectsDowngrade()
    {
        var context = CreateContext(new ReportingProtocolAttribute { RequestKind = ReportingProtocolRequestKind.Upload });
        context.Request.Method = HttpMethods.Post;
        context.Request.Body = JsonBody("{ \"photoTypeId\": \"type\", \"reportPhotoSlotId\": \"slot\" }");
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        Assert.That((await ReadErrorCodeAsync(context)), Is.EqualTo(ReportingProtocolResponses.V2RequiredCode));
    }

    [Test]
    public void ResultFilter_WithBuiltInSchemaTwoSnapshot_AdaptsToLegacyRequiredViews()
    {
        var photoTypeId = LgymApi.Domain.ValueObjects.Id<TrainerPhotoType>.New().ToString();
        var template = CreateSnapshotTemplate(photoTypeId, PhotoTypeNameMode.SystemLocalized, "Front");

        var compatible = ReportingProtocolResultFilter.TryAdapt(template);

        Assert.That(compatible, Is.True);
        var config = template.Fields.Single().ModuleConfig!.Value;
        Assert.That(config.TryGetProperty("schemaVersion", out _), Is.False);
        Assert.That(config.GetProperty("requiredViews").EnumerateArray().Single().GetString(), Is.EqualTo("Front"));
    }

    [TestCase(PhotoTypeNameMode.TrainerProvided, "Front")]
    [TestCase(PhotoTypeNameMode.SystemLocalized, null)]
    public void ResultFilter_WithCustomOrLiteralOverrideSnapshot_RequiresUpgrade(
        PhotoTypeNameMode nameMode,
        string? systemKey)
    {
        var photoTypeId = LgymApi.Domain.ValueObjects.Id<TrainerPhotoType>.New().ToString();
        var template = CreateSnapshotTemplate(photoTypeId, nameMode, systemKey);

        Assert.That(ReportingProtocolResultFilter.TryAdapt(template), Is.False);
    }

    [Test]
    public async Task IdempotencyScopeAndFingerprint_IsolateCanonicalReportingProtocol()
    {
        var p1 = await CaptureIdempotencyRecordAsync(1);
        var p1Again = await CaptureIdempotencyRecordAsync(1);
        var p2 = await CaptureIdempotencyRecordAsync(2);

        Assert.That(p1.ScopeTuple, Is.EqualTo(p1Again.ScopeTuple));
        Assert.That(p1.RequestFingerprint, Is.EqualTo(p1Again.RequestFingerprint));
        Assert.That(p1.ScopeTuple, Does.EndWith("|reporting-protocol:1"));
        Assert.That(p2.ScopeTuple, Does.EndWith("|reporting-protocol:2"));
        Assert.That(p2.ScopeTuple, Is.Not.EqualTo(p1.ScopeTuple));
        Assert.That(p2.RequestFingerprint, Is.Not.EqualTo(p1.RequestFingerprint));
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
