using System.Net;
using System.Reflection;
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
    [TestCase(false)]
    [TestCase(true)]
    public async Task ImmutableWrite_UsesR2CompatiblePutAndConfirmsExactStoredBytes(bool duplicate)
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var client = Substitute.For<IAmazonS3>();
        PutObjectRequest? captured = null;
        byte[]? uploaded = null;
        client.PutObjectAsync(Arg.Do<PutObjectRequest>(request => { captured = request; using var copy = new MemoryStream(); request.InputStream.CopyTo(copy); uploaded = copy.ToArray(); }), Arg.Any<CancellationToken>())
            .Returns(_ => duplicate ? Task.FromException<PutObjectResponse>(new AmazonS3Exception("synthetic existing key") { StatusCode = HttpStatusCode.PreconditionFailed }) : Task.FromResult(new PutObjectResponse()));
        client.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(Response(bytes)));
        using var storage = Create(client);
        await storage.WriteVerifiedAsync("verified/fixture/digest", bytes, "image/gif", default);
        uploaded.Should().Equal(bytes);
        captured!.BucketName.Should().Be("exercise-private"); captured.IfNoneMatch.Should().Be("*");
        captured.DisablePayloadSigning.Should().BeTrue(); captured.DisableDefaultChecksumValidation.Should().BeTrue(); captured.UseChunkEncoding.Should().BeFalse();
        await client.Received(1).GetObjectAsync("exercise-private", "verified/fixture/digest", Arg.Any<CancellationToken>());
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ImmutableWrite_RejectsCorruptReadbackForNewAndDuplicateObjects(bool duplicate)
    {
        var client = Substitute.For<IAmazonS3>();
        client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => duplicate ? Task.FromException<PutObjectResponse>(new AmazonS3Exception("synthetic existing key") { StatusCode = HttpStatusCode.PreconditionFailed }) : Task.FromResult(new PutObjectResponse()));
        client.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(Response([4, 3, 2, 1])));
        using var storage = Create(client);
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(async () => await storage.WriteVerifiedAsync("verified/fixture/digest", [1, 2, 3, 4], "image/gif", default));
    }

    [Test]
    public async Task PublishedWrite_ReturnsPermanentUrlOnlyAfterReadbackAndNeverUsesPrivateBucket()
    {
        var client = Substitute.For<IAmazonS3>();
        var bytes = new byte[] { 1, 2, 3 };
        client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new PutObjectResponse()));
        client.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(Response(bytes)));
        using var storage = Create(client);
        var key = "exercises/00000000-0000-0000-0000-000000000001/" + new string('a', 64) + "/animation.gif";
        var url = await storage.PublishAsync(key, bytes, "image/gif", default);
        url.Should().Be("https://exercise.example.invalid/" + key);
        await client.Received(1).PutObjectAsync(Arg.Is<PutObjectRequest>(x => x.BucketName == "exercise-published" && x.Headers.CacheControl == "public,max-age=31536000,immutable"), Arg.Any<CancellationToken>());
        await client.Received(1).GetObjectAsync("exercise-published", key, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PrivateRead_RejectsOversizedActualStreamDespiteMisleadingMetadata()
    {
        var client = Substitute.For<IAmazonS3>();
        var response = Response(new byte[ExerciseMediaLimits.MaxBytes + 1]); response.ContentLength = 1;
        client.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(response));
        using var storage = Create(client);
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(async () => await storage.ReadPrivateAsync("staging/fixture/source", default));
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(ExerciseMediaLimits.MaxBytes + 1)]
    public async Task PrivateRead_RejectsInvalidDeclaredSizeBeforeReading(int declaredSize)
    {
        var client = Substitute.For<IAmazonS3>();
        var response = Response([1]);
        response.ContentLength = declaredSize;
        client.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(response));
        using var storage = Create(client);
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(async () => await storage.ReadPrivateAsync("staging/fixture/source", default));
    }

    [Test]
    public async Task PrivateRead_RejectsTruncatedObject()
    {
        var client = Substitute.For<IAmazonS3>();
        var response = Response([1]);
        response.ContentLength = 2;
        client.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(response));
        using var storage = Create(client);
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(async () => await storage.ReadPrivateAsync("staging/fixture/source", default));
    }

    [Test]
    public async Task ImmutableWrite_RejectsObjectWithDifferentLength()
    {
        var client = Substitute.For<IAmazonS3>();
        client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new PutObjectResponse()));
        client.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(Response([1])));
        using var storage = Create(client);
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(async () => await storage.WriteVerifiedAsync("verified/fixture/digest", [1, 2], "image/gif", default));
    }

    [TestCase("staging/private/source")]
    [TestCase("exercises/00000000-0000-0000-0000-000000000001/animation.gif")]
    [TestCase("exercises/00000000-0000-0000-0000-000000000001/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/source.gif")]
    [TestCase("exercises/00000000-0000-0000-0000-000000000001/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/image.svg")]
    public async Task Publish_RejectsKeysOutsideSanitizedPublicLayoutBeforeStorage(string key)
    {
        var client = Substitute.For<IAmazonS3>();
        using var storage = Create(client);
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(async () => await storage.PublishAsync(key, [1], "image/gif", default));
        await client.DidNotReceive().PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void SignedPut_RespectsCancellationBeforeCreatingBearerUrl()
    {
        var client = Substitute.For<IAmazonS3>();
        using var storage = Create(client);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Action sign = () => storage.SignUploadAsync("staging/fixture/source", "image/gif", DateTimeOffset.UtcNow.AddMinutes(5), cancellation.Token);
        sign.Should().Throw<OperationCanceledException>();
    }

    private static GetObjectResponse Response(byte[] bytes) => new() { ContentLength = bytes.Length, ResponseStream = new MemoryStream(bytes, false) };

    private static ExerciseMediaStorage Create(IAmazonS3 client)
    {
        var storage = new ExerciseMediaStorage(new ExerciseMediaStorageOptions { Enabled = true, Endpoint = "https://technical-account.r2.cloudflarestorage.com", AccessKeyId = "synthetic-exercise-access", SecretAccessKey = "synthetic-exercise-secret", PrivateBucket = "exercise-private", PublishedBucket = "exercise-published", PublicBaseUrl = "https://exercise.example.invalid" });
        // Match the established R2 adapter-test seam without adding a second production SDK registration.
        var field = typeof(ExerciseMediaStorage).GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((IDisposable?)field.GetValue(storage))?.Dispose(); field.SetValue(storage, client);
        return storage;
    }
}
