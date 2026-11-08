using System.Text;
using System.Text.Json;
using FluentAssertions;
using LgymApi.Api.Idempotency;
using LgymApi.Api.Middleware;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ApiIdempotencyMiddlewareCancellationTests
{
    [Test]
    public async Task InvokeAsync_WithoutIdempotencyKey_PreservesBadRequestContract()
    {
        await using var responseBody = new MemoryStream();
        var context = CreateContext(responseBody);
        var repository = Substitute.For<IApiIdempotencyRecordRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var nextCalled = false;
        var middleware = new ApiIdempotencyMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, repository, unitOfWork);

        var body = await ReadBodyAsync(responseBody);
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        AssertErrorBody(body, "Idempotency key is required.", "IDEMPOTENCY_KEY_REQUIRED");
        nextCalled.Should().BeFalse();
        TestContext.Progress.WriteLine($"missing-key status={context.Response.StatusCode} body={body}");
    }

    [Test]
    public async Task InvokeAsync_WithReusedKeyAndDifferentFingerprint_PreservesConflictContract()
    {
        await using var requestBody = new MemoryStream();
        await using var responseBody = new MemoryStream();
        var context = CreateContext(responseBody, requestBody);
        context.Request.Headers[ApiIdempotencyHeaders.IdempotencyKey] = "conflict-key";
        var repository = Substitute.For<IApiIdempotencyRecordRepository>();
        repository
            .FindByScopeAndKeyAsync(Arg.Any<string>(), "conflict-key", context.RequestAborted)
            .Returns(CreateCompletedRecord("conflict-key", "different-fingerprint"));
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var nextCalled = false;
        var middleware = new ApiIdempotencyMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, repository, unitOfWork);

        var body = await ReadBodyAsync(responseBody);
        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        AssertErrorBody(
            body,
            "Idempotency key reused with different request payload.",
            "IDEMPOTENCY_KEY_FINGERPRINT_MISMATCH");
        nextCalled.Should().BeFalse();
        TestContext.Progress.WriteLine($"conflict status={context.Response.StatusCode} body={body}");
    }

    [TestCase(null)]
    [TestCase("   ")]
    public async Task InvokeAsync_WithMissingOrWhitespaceKey_PropagatesCancellationToErrorWrite(string? key)
    {
        using var cancellationSource = new CancellationTokenSource();
        await using var responseBody = new RecordingMemoryStream(cancellationSource);
        var context = CreateContext(responseBody);
        context.RequestAborted = cancellationSource.Token;
        if (key != null)
        {
            context.Request.Headers[ApiIdempotencyHeaders.IdempotencyKey] = key;
        }

        var repository = Substitute.For<IApiIdempotencyRecordRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var middleware = new ApiIdempotencyMiddleware(_ => Task.CompletedTask);

        var action = () => middleware.InvokeAsync(context, repository, unitOfWork);

        await action.Should().ThrowAsync<OperationCanceledException>();
        responseBody.WriteTokens.Should().NotBeEmpty();
        responseBody.ObservedRequestCancellation.Should().BeTrue();
    }

    [Test]
    public async Task InvokeAsync_WithFingerprintConflict_PropagatesCancellationToErrorWrite()
    {
        await using var requestBody = new MemoryStream();
        using var cancellationSource = new CancellationTokenSource();
        await using var responseBody = new RecordingMemoryStream(cancellationSource);
        var context = CreateContext(responseBody, requestBody);
        context.RequestAborted = cancellationSource.Token;
        context.Request.Headers[ApiIdempotencyHeaders.IdempotencyKey] = "conflict-key";
        var repository = Substitute.For<IApiIdempotencyRecordRepository>();
        repository
            .FindByScopeAndKeyAsync(Arg.Any<string>(), "conflict-key", cancellationSource.Token)
            .Returns(CreateCompletedRecord("conflict-key", "different-fingerprint"));
        var middleware = new ApiIdempotencyMiddleware(_ => Task.CompletedTask);

        var action = () => middleware.InvokeAsync(context, repository, Substitute.For<IUnitOfWork>());

        await action.Should().ThrowAsync<OperationCanceledException>();
        responseBody.WriteTokens.Should().NotBeEmpty();
        responseBody.ObservedRequestCancellation.Should().BeTrue();
    }

    [Test]
    public async Task InvokeAsync_WithAuthenticatedUser_PropagatesCancellationToFingerprintReadAndPreservesFlow()
    {
        await using var requestBody = new RecordingMemoryStream("{\"value\":1}");
        await using var responseBody = new RecordingMemoryStream();
        using var cancellationSource = new CancellationTokenSource();
        var context = CreateContext(responseBody, requestBody);
        context.RequestAborted = cancellationSource.Token;
        context.Request.Headers[ApiIdempotencyHeaders.IdempotencyKey] = "new-key";
        var repository = Substitute.For<IApiIdempotencyRecordRepository>();
        repository
            .AddOrGetExistingAsync(Arg.Any<ApiIdempotencyRecord>(), cancellationSource.Token)
            .Returns(call => call.Arg<ApiIdempotencyRecord>());
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(cancellationSource.Token).Returns(1);
        var nextCalls = 0;
        var middleware = new ApiIdempotencyMiddleware(async nextContext =>
        {
            nextCalls++;
            await nextContext.Response.WriteAsync("{\"ok\":true}", nextContext.RequestAborted);
        });

        await middleware.InvokeAsync(context, repository, unitOfWork);

        requestBody.ReadTokens.Should().NotBeEmpty();
        requestBody.ReadTokens.Should().OnlyContain(token => token == cancellationSource.Token);
        requestBody.Position.Should().Be(0);
        nextCalls.Should().Be(1);
        context.Response.Body.Should().BeSameAs(responseBody);
        (await ReadBodyAsync(responseBody)).Should().Be("{\"ok\":true}");
        await repository.Received(1).UpdateAsync(
            Arg.Is<ApiIdempotencyRecord>(record => record.ResponseBodyJson == "{\"ok\":true}"),
            cancellationSource.Token);
        await unitOfWork.Received(2).SaveChangesAsync(cancellationSource.Token);
    }

    [Test]
    public async Task InvokeAsync_WithNormalizedEmail_PropagatesCancellationToBothRequestBodyReads()
    {
        await using var requestBody = new RecordingMemoryStream("{\"email\":\" User@Example.COM \"}");
        await using var responseBody = new RecordingMemoryStream();
        using var cancellationSource = new CancellationTokenSource();
        var context = CreateContext(
            responseBody,
            requestBody,
            ApiIdempotencyScopeSource.NormalizedEmail);
        context.RequestAborted = cancellationSource.Token;
        context.Request.Headers[ApiIdempotencyHeaders.IdempotencyKey] = "email-key";
        var repository = Substitute.For<IApiIdempotencyRecordRepository>();
        repository
            .FindByScopeAndKeyAsync(
                "POST|/api/idempotency-test|user@example.com",
                "email-key",
                cancellationSource.Token)
            .Returns(CreateCompletedRecord("email-key", "different-fingerprint"));
        var middleware = new ApiIdempotencyMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, repository, Substitute.For<IUnitOfWork>());

        requestBody.ReadTokens.Should().HaveCountGreaterThanOrEqualTo(2);
        requestBody.ReadTokens.Should().OnlyContain(token => token == cancellationSource.Token);
        requestBody.Position.Should().Be(0);
        await repository.Received(1).FindByScopeAndKeyAsync(
            "POST|/api/idempotency-test|user@example.com",
            "email-key",
            cancellationSource.Token);
    }

    [Test]
    public async Task InvokeAsync_WithPreCancelledRequest_DoesNotMutatePersistenceOrInvokeNext()
    {
        await using var requestBody = new RecordingMemoryStream("{\"value\":1}");
        await using var responseBody = new RecordingMemoryStream();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var context = CreateContext(responseBody, requestBody);
        context.RequestAborted = cancellationSource.Token;
        context.Request.Headers[ApiIdempotencyHeaders.IdempotencyKey] = "cancelled-key";
        var repository = Substitute.For<IApiIdempotencyRecordRepository>();
        repository
            .AddOrGetExistingAsync(Arg.Any<ApiIdempotencyRecord>(), cancellationSource.Token)
            .Returns(call => call.Arg<ApiIdempotencyRecord>());
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(cancellationSource.Token).Returns(1);
        var nextCalls = 0;
        var middleware = new ApiIdempotencyMiddleware(_ =>
        {
            nextCalls++;
            return Task.CompletedTask;
        });

        var action = () => middleware.InvokeAsync(context, repository, unitOfWork);

        await action.Should().ThrowAsync<OperationCanceledException>();
        nextCalls.Should().Be(0);
        context.Response.Body.Should().BeSameAs(responseBody);
        await repository.DidNotReceiveWithAnyArgs().FindByScopeAndKeyAsync(default!, default!, default);
        await repository.DidNotReceiveWithAnyArgs().AddOrGetExistingAsync(default!, default);
        await repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
        await unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        TestContext.Progress.WriteLine(
            $"pre-cancelled cancelled=true nextCalls={nextCalls} persistenceCalls=0 responseRestored={ReferenceEquals(context.Response.Body, responseBody)}");
    }

    [Test]
    public async Task InvokeAsync_WhenExistingRecordIsInProgress_DoesNotInvokeMutation()
    {
        await using var requestBody = new MemoryStream(Encoding.UTF8.GetBytes("{\"value\":1}"));
        await using var responseBody = new MemoryStream();
        var context = CreateContext(responseBody, requestBody);
        context.Request.Headers[ApiIdempotencyHeaders.IdempotencyKey] = "in-progress-key";
        var repository = Substitute.For<IApiIdempotencyRecordRepository>();
        repository
            .FindByScopeAndKeyAsync(Arg.Any<string>(), "in-progress-key", context.RequestAborted)
            .Returns(new ApiIdempotencyRecord
            {
                Id = Id<ApiIdempotencyRecord>.New(),
                IdempotencyKey = "in-progress-key",
                ScopeTuple = "scope",
                RequestFingerprint = "fingerprint",
                ResponseStatusCode = 0,
                ResponseBodyJson = string.Empty,
                ProcessedAt = DateTimeOffset.UtcNow
            });
        var nextCalls = 0;
        var middleware = new ApiIdempotencyMiddleware(_ =>
        {
            nextCalls++;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, repository, Substitute.For<IUnitOfWork>());

        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        nextCalls.Should().Be(0);
        await repository.DidNotReceiveWithAnyArgs().AddOrGetExistingAsync(default!, default);
        await repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Test]
    public async Task InvokeAsync_WhenRaceReturnsCompletedPrivateRecord_DeniesReplayWithoutInvokingMutation()
    {
        await using var requestBody = new MemoryStream(Encoding.UTF8.GetBytes("{\"value\":1}"));
        await using var responseBody = new MemoryStream();
        var context = CreateContext(responseBody, requestBody, replayOwner: ApiIdempotencyReplayOwner.Identity);
        context.Request.Headers[ApiIdempotencyHeaders.IdempotencyKey] = "race-key";
        context.Features.Set<IAuthenticatedAccountContextFeature>(
            new TestAuthenticatedAccountContextFeature(
                new AuthenticatedAccountContext(Id<AccountReference>.New(), Id<AccountSessionReference>.New(), [], [], false, false)));
        var repository = Substitute.For<IApiIdempotencyRecordRepository>();
        repository
            .AddOrGetExistingAsync(Arg.Any<ApiIdempotencyRecord>(), context.RequestAborted)
            .Returns(call =>
            {
                var proposed = call.Arg<ApiIdempotencyRecord>();
                return new ApiIdempotencyRecord
                {
                    Id = Id<ApiIdempotencyRecord>.New(),
                    IdempotencyKey = proposed.IdempotencyKey,
                    ScopeTuple = proposed.ScopeTuple,
                    RequestFingerprint = proposed.RequestFingerprint,
                    ResponseStatusCode = StatusCodes.Status200OK,
                    ResponseBodyJson = "{\"private\":true}",
                    ProcessedAt = DateTimeOffset.UtcNow
                };
            });
        var deniedPort = new TestReplayAccessPort(canReplay: false);
        var nextCalls = 0;
        var middleware = new ApiIdempotencyMiddleware(_ =>
        {
            nextCalls++;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(
            context,
            repository,
            Substitute.For<IUnitOfWork>(),
            new ApiIdempotencyReplayAccessEvaluator([deniedPort]));

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await ReadBodyAsync(responseBody)).Should().NotContain("private");
        nextCalls.Should().Be(0);
        deniedPort.Calls.Should().Be(1);
        await repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Test]
    public async Task InvokeAsync_WhenConcurrentClaimCommitFindsPrivateWinner_DeniesReplayWithoutInvokingMutation()
    {
        await using var requestBody = new MemoryStream(Encoding.UTF8.GetBytes("{\"value\":1}"));
        await using var responseBody = new MemoryStream();
        var context = CreateContext(responseBody, requestBody, replayOwner: ApiIdempotencyReplayOwner.Identity);
        context.Request.Headers[ApiIdempotencyHeaders.IdempotencyKey] = "concurrent-key";
        context.Features.Set<IAuthenticatedAccountContextFeature>(
            new TestAuthenticatedAccountContextFeature(
                new AuthenticatedAccountContext(Id<AccountReference>.New(), Id<AccountSessionReference>.New(), [], [], false, false)));
        var repository = Substitute.For<IApiIdempotencyRecordRepository>();
        repository
            .AddOrGetExistingAsync(Arg.Any<ApiIdempotencyRecord>(), context.RequestAborted)
            .Returns(call => call.Arg<ApiIdempotencyRecord>());
        repository
            .TryRecoverDuplicateAsync(Arg.Any<ApiIdempotencyRecord>(), Arg.Any<DbUpdateException>(), context.RequestAborted)
            .Returns(call =>
            {
                var proposed = call.ArgAt<ApiIdempotencyRecord>(0);
                return new ApiIdempotencyRecord
                {
                    Id = Id<ApiIdempotencyRecord>.New(),
                    IdempotencyKey = proposed.IdempotencyKey,
                    ScopeTuple = proposed.ScopeTuple,
                    RequestFingerprint = proposed.RequestFingerprint,
                    ResponseStatusCode = StatusCodes.Status200OK,
                    ResponseBodyJson = "{\"private\":true}",
                    ProcessedAt = DateTimeOffset.UtcNow
                };
            });
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(context.RequestAborted).Returns(Task.FromException<int>(new DbUpdateException()));
        var deniedPort = new TestReplayAccessPort(canReplay: false);
        var nextCalls = 0;
        var middleware = new ApiIdempotencyMiddleware(_ =>
        {
            nextCalls++;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(
            context,
            repository,
            unitOfWork,
            new ApiIdempotencyReplayAccessEvaluator([deniedPort]));

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await ReadBodyAsync(responseBody)).Should().NotContain("private");
        nextCalls.Should().Be(0);
        deniedPort.Calls.Should().Be(1);
        await repository.Received(1).TryRecoverDuplicateAsync(
            Arg.Any<ApiIdempotencyRecord>(),
            Arg.Any<DbUpdateException>(),
            context.RequestAborted);
        await repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Test]
    public async Task InvokeAsync_WhenConcurrentClaimCommitFindsInProgressWinner_ReturnsConflictWithoutInvokingMutation()
    {
        await using var requestBody = new MemoryStream(Encoding.UTF8.GetBytes("{\"value\":1}"));
        await using var responseBody = new MemoryStream();
        var context = CreateContext(responseBody, requestBody);
        context.Request.Headers[ApiIdempotencyHeaders.IdempotencyKey] = "in-progress-race-key";
        var repository = Substitute.For<IApiIdempotencyRecordRepository>();
        repository
            .AddOrGetExistingAsync(Arg.Any<ApiIdempotencyRecord>(), context.RequestAborted)
            .Returns(call => call.Arg<ApiIdempotencyRecord>());
        repository
            .TryRecoverDuplicateAsync(Arg.Any<ApiIdempotencyRecord>(), Arg.Any<DbUpdateException>(), context.RequestAborted)
            .Returns(call =>
            {
                var proposed = call.ArgAt<ApiIdempotencyRecord>(0);
                return new ApiIdempotencyRecord
                {
                    Id = Id<ApiIdempotencyRecord>.New(),
                    IdempotencyKey = proposed.IdempotencyKey,
                    ScopeTuple = proposed.ScopeTuple,
                    RequestFingerprint = proposed.RequestFingerprint,
                    ResponseStatusCode = 0,
                    ResponseBodyJson = string.Empty,
                    ProcessedAt = DateTimeOffset.UtcNow
                };
            });
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(context.RequestAborted).Returns(Task.FromException<int>(new DbUpdateException()));
        var nextCalls = 0;
        var middleware = new ApiIdempotencyMiddleware(_ =>
        {
            nextCalls++;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, repository, unitOfWork);

        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        nextCalls.Should().Be(0);
        await repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Test]
    public async Task InvokeAsync_WhenConcurrentClaimCommitFailureIsNotRecoverable_Rethrows()
    {
        await using var requestBody = new MemoryStream(Encoding.UTF8.GetBytes("{\"value\":1}"));
        await using var responseBody = new MemoryStream();
        var context = CreateContext(responseBody, requestBody);
        context.Request.Headers[ApiIdempotencyHeaders.IdempotencyKey] = "non-recoverable-race-key";
        var repository = Substitute.For<IApiIdempotencyRecordRepository>();
        repository
            .AddOrGetExistingAsync(Arg.Any<ApiIdempotencyRecord>(), context.RequestAborted)
            .Returns(call => call.Arg<ApiIdempotencyRecord>());
        repository
            .TryRecoverDuplicateAsync(Arg.Any<ApiIdempotencyRecord>(), Arg.Any<DbUpdateException>(), context.RequestAborted)
            .Returns((ApiIdempotencyRecord?)null);
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(context.RequestAborted).Returns(Task.FromException<int>(new DbUpdateException()));
        var middleware = new ApiIdempotencyMiddleware(_ => Task.CompletedTask);

        var action = () => middleware.InvokeAsync(context, repository, unitOfWork);

        await action.Should().ThrowAsync<DbUpdateException>();
        await repository.Received(1).TryRecoverDuplicateAsync(
            Arg.Any<ApiIdempotencyRecord>(),
            Arg.Any<DbUpdateException>(),
            context.RequestAborted);
    }

    private static DefaultHttpContext CreateContext(
        Stream responseBody,
        Stream? requestBody = null,
        ApiIdempotencyScopeSource scopeSource = ApiIdempotencyScopeSource.AuthenticatedUser,
        ApiIdempotencyReplayOwner replayOwner = ApiIdempotencyReplayOwner.Public)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Body = requestBody ?? Stream.Null;
        context.Response.Body = responseBody;
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new ApiIdempotencyAttribute(
                "/api/idempotency-test",
                scopeSource,
                replayOwner)),
            "Idempotency test"));
        return context;
    }

    private static ApiIdempotencyRecord CreateCompletedRecord(string key, string fingerprint) => new()
    {
        Id = Id<ApiIdempotencyRecord>.New(),
        IdempotencyKey = key,
        ScopeTuple = "POST|/api/idempotency-test|anonymous",
        RequestFingerprint = fingerprint,
        ResponseStatusCode = StatusCodes.Status200OK,
        ResponseBodyJson = "{}",
        ProcessedAt = DateTimeOffset.UtcNow
    };

    private static async Task<string> ReadBodyAsync(Stream body)
    {
        body.Position = 0;
        using var reader = new StreamReader(body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private static void AssertErrorBody(string body, string message, string code)
    {
        using var document = JsonDocument.Parse(body);
        var properties = document.RootElement.EnumerateObject().ToArray();
        properties.Select(property => property.Name).Should().Equal("message", "code");
        document.RootElement.GetProperty("message").GetString().Should().Be(message);
        document.RootElement.GetProperty("code").GetString().Should().Be(code);
    }

    private sealed class RecordingMemoryStream : MemoryStream
    {
        private readonly CancellationTokenSource? _requestCancellationSource;

        public RecordingMemoryStream()
        {
        }

        public RecordingMemoryStream(CancellationTokenSource requestCancellationSource)
        {
            _requestCancellationSource = requestCancellationSource;
        }

        public RecordingMemoryStream(string content)
            : base(Encoding.UTF8.GetBytes(content))
        {
        }

        public List<CancellationToken> ReadTokens { get; } = [];

        public List<CancellationToken> WriteTokens { get; } = [];

        public bool ObservedRequestCancellation { get; private set; }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            ReadTokens.Add(cancellationToken);
            return base.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ReadTokens.Add(cancellationToken);
            return base.ReadAsync(buffer, cancellationToken);
        }

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            WriteTokens.Add(cancellationToken);
            CancelRequestAndObserve(cancellationToken);
            return base.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            WriteTokens.Add(cancellationToken);
            CancelRequestAndObserve(cancellationToken);
            return base.WriteAsync(buffer, cancellationToken);
        }

        private void CancelRequestAndObserve(CancellationToken cancellationToken)
        {
            if (_requestCancellationSource == null)
            {
                return;
            }

            _requestCancellationSource.Cancel();
            ObservedRequestCancellation = cancellationToken.IsCancellationRequested;
        }
    }

    private sealed record TestAuthenticatedAccountContextFeature(AuthenticatedAccountContext Context)
        : IAuthenticatedAccountContextFeature;

    private sealed class TestReplayAccessPort(bool canReplay) : IApiIdempotencyReplayAccessPort
    {
        public int Calls { get; private set; }

        public ApiIdempotencyReplayOwner Owner => ApiIdempotencyReplayOwner.Identity;

        public Task<bool> CanReplayAsync(
            ApiIdempotencyReplayAccessContext context,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(canReplay);
        }
    }
}
