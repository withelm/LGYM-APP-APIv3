using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Infrastructure.Options;
using LgymApi.Infrastructure.Services;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ExerciseMediaStorageTests
{
    private const string Bucket = "tenant-one-assets";
    private static string Key(byte[] bytes) => "exercises/00000000-0000-0000-0000-000000000001/" + Convert.ToHexStringLower(SHA256.HashData(bytes)) + "/animation.gif";

    [TestCase(false)]
    [TestCase(true)]
    public async Task ImmutablePublish_UsesOneBucketAndR2CompatiblePutThenConfirmsStoredBytes(bool duplicate)
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var key = Key(bytes);
        var client = Substitute.For<IAmazonS3>();
        PutObjectRequest? captured = null;
        byte[]? uploaded = null;
        using var cancellation = new CancellationTokenSource();
        client.PutObjectAsync(Arg.Do<PutObjectRequest>(request =>
        {
            captured = request;
            using var copy = new MemoryStream();
            request.InputStream.CopyTo(copy);
            uploaded = copy.ToArray();
        }), cancellation.Token).Returns(_ => duplicate
            ? Task.FromException<PutObjectResponse>(new AmazonS3Exception("synthetic existing key") { StatusCode = HttpStatusCode.PreconditionFailed })
            : Task.FromResult(new PutObjectResponse()));
        client.GetObjectAsync(Bucket, key, cancellation.Token).Returns(_ => Task.FromResult(Response(bytes)));
        using var storage = Create(client);

        var url = await storage.PublishAsync(key, bytes, "image/gif", cancellation.Token);

        url.Should().Be("https://exercises-dev.owner.test/" + key);
        uploaded.Should().Equal(bytes);
        captured!.BucketName.Should().Be(Bucket);
        captured.Key.Should().Be(key);
        captured.ContentType.Should().Be("image/gif");
        captured.IfNoneMatch.Should().Be("*");
        captured.DisablePayloadSigning.Should().BeTrue();
        captured.DisableDefaultChecksumValidation.Should().BeTrue();
        captured.UseChunkEncoding.Should().BeFalse();
        captured.Headers.CacheControl.Should().Be("public,max-age=31536000,immutable");
        await client.Received(1).GetObjectAsync(Bucket, key, cancellation.Token);
        client.ReceivedCalls().Should().OnlyContain(call => call.GetMethodInfo().Name == "PutObjectAsync" || call.GetMethodInfo().Name == "GetObjectAsync");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ImmutablePublish_RejectsSameLengthCorruptReadbackForNewAndDuplicateObjects(bool duplicate)
    {
        var client = Substitute.For<IAmazonS3>();
        client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>()).Returns(_ => duplicate
            ? Task.FromException<PutObjectResponse>(new AmazonS3Exception("synthetic existing key") { StatusCode = HttpStatusCode.PreconditionFailed })
            : Task.FromResult(new PutObjectResponse()));
        client.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(Response([4, 3, 2, 1])));
        using var storage = Create(client);
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(async () => await storage.PublishAsync(Key([1, 2, 3, 4]), [1, 2, 3, 4], "image/gif", default));
    }

    [Test]
    public async Task ImmutablePublish_RejectsDifferentLengthReadback()
    {
        var client = Substitute.For<IAmazonS3>();
        client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new PutObjectResponse()));
        client.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(Response([1])));
        using var storage = Create(client);
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(async () => await storage.PublishAsync(Key([1, 2]), [1, 2], "image/gif", default));
    }

    [Test]
    public async Task ProviderWriteFailure_IsNotMistakenForAnExistingImmutableObject()
    {
        var client = Substitute.For<IAmazonS3>();
        client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<PutObjectResponse>(new AmazonS3Exception("synthetic forbidden") { StatusCode = HttpStatusCode.Forbidden }));
        using var storage = Create(client);
        await Assert.ThrowsAsync<AmazonS3Exception>(async () => await storage.PublishAsync(Key([1]), [1], "image/gif", default));
        await client.DidNotReceive().GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestCase(1)]
    [TestCase(ExerciseMediaLimits.MaxBytes)]
    public async Task Read_UsesOneBucketAndAcceptsExactBoundary(int size)
    {
        var bytes = new byte[size];
        var key = Key(bytes);
        var client = Substitute.For<IAmazonS3>();
        using var cancellation = new CancellationTokenSource();
        client.GetObjectAsync(Bucket, key, cancellation.Token).Returns(_ => Task.FromResult(Response(bytes)));
        using var storage = Create(client);
        var read = await storage.ReadAsync(key, cancellation.Token);
        read.Should().Equal(bytes);
        await client.Received(1).GetObjectAsync(Bucket, key, cancellation.Token);
    }

    [Test]
    public async Task Read_RejectsOversizedStreamDespiteMisleadingDeclaredLength()
    {
        var client = Substitute.For<IAmazonS3>();
        var response = Response(new byte[ExerciseMediaLimits.MaxBytes + 1]);
        response.ContentLength = 1;
        client.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(response));
        using var storage = Create(client);
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(async () => await storage.ReadAsync(Key([1]), default));
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(ExerciseMediaLimits.MaxBytes + 1)]
    public async Task Read_RejectsInvalidDeclaredLength(int declaredSize)
    {
        var client = Substitute.For<IAmazonS3>();
        var response = Response([1]);
        response.ContentLength = declaredSize;
        client.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(response));
        using var storage = Create(client);
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(async () => await storage.ReadAsync(Key([1]), default));
    }

    [Test]
    public async Task Read_RejectsTruncatedObject()
    {
        var client = Substitute.For<IAmazonS3>();
        var response = Response([1]);
        response.ContentLength = 2;
        client.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(response));
        using var storage = Create(client);
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(async () => await storage.ReadAsync(Key([1]), default));
    }

    [Test]
    public async Task Read_MissingObjectReturnsNullButOtherProviderFailuresPropagate()
    {
        var client = Substitute.For<IAmazonS3>();
        client.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<GetObjectResponse>(new AmazonS3Exception("synthetic missing") { StatusCode = HttpStatusCode.NotFound }));
        using var storage = Create(client);
        (await storage.ReadAsync(Key([1]), default)).Should().BeNull();
        client.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<GetObjectResponse>(new AmazonS3Exception("synthetic forbidden") { StatusCode = HttpStatusCode.Forbidden }));
        await Assert.ThrowsAsync<AmazonS3Exception>(async () => await storage.ReadAsync(Key([1]), default));
    }

    [TestCase("exercises/------------------------------------/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/animation.gif")]
    [TestCase("staging/private/source")]
    [TestCase("verified/private/source")]
    [TestCase("reports/private/photo.jpg")]
    [TestCase("exercises/00000000-0000-0000-0000-000000000001/animation.gif")]
    [TestCase("exercises/00000000-0000-0000-0000-000000000001/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/source.gif")]
    [TestCase("exercises/00000000-0000-0000-0000-000000000001/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/image.svg")]
    public async Task EveryStorageOperation_RejectsKeysOutsideImmutableExerciseLayout(string key)
    {
        var client = Substitute.For<IAmazonS3>();
        using var storage = Create(client);
        Action url = () => storage.GetPublicUrl(key);
        url.Should().Throw<ExerciseMediaRejectedException>();
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(async () => await storage.PublishAsync(key, [1], "image/gif", default));
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(async () => await storage.ReadAsync(key, default));
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(async () => await storage.DeleteAsync(key, default));
        client.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task CleanupDelete_UsesOnlyTheOneExerciseBucketAndCallerCancellation()
    {
        var client = Substitute.For<IAmazonS3>();
        using var cancellation = new CancellationTokenSource();
        var key = Key([1]);
        client.DeleteObjectAsync(Bucket, key, cancellation.Token).Returns(Task.FromResult(new DeleteObjectResponse()));
        using var storage = Create(client);
        await storage.DeleteAsync(key, cancellation.Token);
        await client.Received(1).DeleteObjectAsync(Bucket, key, cancellation.Token);
    }

    [TestCase("read")]
    [TestCase("publish")]
    [TestCase("delete")]
    public async Task ProviderCancellation_PropagatesWithoutFallback(string operation)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var token = cancellation.Token;
        var key = Key([1]);
        var client = Substitute.For<IAmazonS3>();
        client.GetObjectAsync(Bucket, key, token).Returns(Task.FromCanceled<GetObjectResponse>(token));
        client.PutObjectAsync(Arg.Any<PutObjectRequest>(), token).Returns(Task.FromCanceled<PutObjectResponse>(token));
        client.DeleteObjectAsync(Bucket, key, token).Returns(Task.FromCanceled<DeleteObjectResponse>(token));
        using var storage = Create(client);
        Func<Task> act = operation switch
        {
            "read" => async () => { await storage.ReadAsync(key, token); },
            "publish" => async () => { await storage.PublishAsync(key, [1], "image/gif", token); },
            _ => () => storage.DeleteAsync(key, token)
        };
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static GetObjectResponse Response(byte[] bytes) => new() { ContentLength = bytes.Length, ResponseStream = new MemoryStream(bytes, false) };

    private static ExerciseMediaStorage Create(IAmazonS3 client)
    {
        var storage = new ExerciseMediaStorage(new ExerciseMediaStorageOptions
        {
            Enabled = true, Endpoint = "https://technical-account.r2.cloudflarestorage.com",
            AccessKeyId = "synthetic-exercise-access", SecretAccessKey = "synthetic-exercise-secret",
            Bucket = Bucket, PublicBaseUrl = "https://exercises-dev.owner.test"
        });
        // Reuse the existing SDK test seam; no second production SDK registration.
        var field = typeof(ExerciseMediaStorage).GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((IDisposable?)field.GetValue(storage))?.Dispose();
        field.SetValue(storage, client);
        return storage;
    }
}
