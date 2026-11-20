using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using LgymApi.Infrastructure.Options;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Infrastructure.Services;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ExerciseMediaStorageTests
{
    private const string Bucket = "tenant-one-assets";
    private static string Key(byte[] bytes, string filename = "animation.gif") => "exercises/00000000-0000-0000-0000-000000000001/" + Convert.ToHexStringLower(SHA256.HashData(bytes)) + "/" + filename;

    [TestCase("animation.gif", "image/gif")]
    [TestCase("image.png", "image/png")]
    [TestCase("image.jpg", "image/jpeg")]
    [TestCase("image.webp", "image/webp")]
    public async Task Publish_UsesConfiguredBucketVerifiedMimeAndR2CompatibleImmutablePut(string filename, string contentType)
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
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
        }), cancellation.Token).Returns(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });
        using var storage = Create(client);
        var url = await storage.PublishAsync(Key(bytes, filename), bytes, contentType, cancellation.Token);
        Assert.That(url, Is.EqualTo("https://exercises-dev.owner.test/" + Key(bytes, filename)));
        Assert.That(uploaded, Is.EqualTo(bytes));
        Assert.That(captured!.BucketName, Is.EqualTo(Bucket));
        Assert.That(captured.Key, Is.EqualTo(Key(bytes, filename)));
        Assert.That(captured.ContentType, Is.EqualTo(contentType));
        Assert.That(captured.IfNoneMatch, Is.EqualTo("*"));
        Assert.That(captured.DisablePayloadSigning, Is.True);
        Assert.That(captured.DisableDefaultChecksumValidation, Is.True);
        Assert.That(captured.UseChunkEncoding, Is.False);
        Assert.That(captured.Headers.CacheControl, Is.EqualTo("public,max-age=31536000,immutable"));
        Assert.That((client.ReceivedCalls()).All(call => call.GetMethodInfo().Name == "PutObjectAsync"), Is.True);
    }

    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    public async Task Publish_PropagatesStorageFailureWithoutReadbackOrSuccess(HttpStatusCode status)
    {
        var client = Substitute.For<IAmazonS3>();
        client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<PutObjectResponse>(new AmazonS3Exception("synthetic failure") { StatusCode = status }));
        using var storage = Create(client);
        await Assert.ThrowsAsync<AmazonS3Exception>(async () => await storage.PublishAsync(Key([1]), [1], "image/gif", default));
        Assert.That((client.ReceivedCalls()).All(call => call.GetMethodInfo().Name == "PutObjectAsync"), Is.True);
    }

    [Test]
    public async Task Publish_RequiresConfirmedSuccessfulPut()
    {
        var client = Substitute.For<IAmazonS3>();
        client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>()).Returns(new PutObjectResponse { HttpStatusCode = HttpStatusCode.ServiceUnavailable });
        using var storage = Create(client);
        await Assert.ThrowsAsync<IOException>(async () => await storage.PublishAsync(Key([1]), [1], "image/gif", default));
    }

    [Test]
    public async Task Publish_PassesCancellation()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var client = Substitute.For<IAmazonS3>();
        client.PutObjectAsync(Arg.Any<PutObjectRequest>(), cancellation.Token).Returns(Task.FromCanceled<PutObjectResponse>(cancellation.Token));
        using var storage = Create(client);
        Func<Task> act = async () => await storage.PublishAsync(Key([1]), [1], "image/gif", cancellation.Token);
        await Assert.ThatAsync(async () => { await act(); }, Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task ReplayAfterConfirmedPut_VerifiesExistingBytes_AndReturnsSameImmutableUrl()
    {
        byte[] bytes = [1, 2, 3, 4];
        var client = Substitute.For<IAmazonS3>();
        client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PutObjectResponse>(new AmazonS3Exception("already exists") { StatusCode = HttpStatusCode.PreconditionFailed }));
        client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>()).Returns(new GetObjectResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            ContentLength = bytes.Length,
            Headers = { ContentType = "image/gif" },
            ResponseStream = new MemoryStream(bytes)
        });
        using var storage = Create(client);
        Assert.That((await storage.PublishAsync(Key(bytes), bytes, "image/gif", default)), Is.EqualTo("https://exercises-dev.owner.test/" + Key(bytes)));
        await client.Received(1).GetObjectAsync(Arg.Is<GetObjectRequest>(request => request.Key == Key(bytes) && request.BucketName == Bucket), Arg.Any<CancellationToken>());
        Assert.That((client.ReceivedCalls()).All(call => call.GetMethodInfo().Name == "PutObjectAsync" || call.GetMethodInfo().Name == "GetObjectAsync"), Is.True);
    }

    [TestCase("bytes")]
    [TestCase("length")]
    [TestCase("mime")]
    [TestCase("truncated")]
    [TestCase("extra")]
    public async Task Replay_NeverAcknowledgesOrOverwritesMismatchedStoredContent(string mismatch)
    {
        byte[] bytes = [1, 2, 3, 4];
        var client = Substitute.For<IAmazonS3>();
        client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PutObjectResponse>(new AmazonS3Exception("already exists") { StatusCode = HttpStatusCode.PreconditionFailed }));
        client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>()).Returns(new GetObjectResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            ContentLength = mismatch == "length" ? 9 : bytes.Length,
            Headers = { ContentType = mismatch == "mime" ? "image/jpeg" : "image/gif" },
            ResponseStream = new MemoryStream(mismatch switch
            {
                "bytes" => [9, 8, 7, 6],
                "truncated" => [1, 2],
                "extra" => [1, 2, 3, 4, 5],
                _ => bytes
            })
        });
        using var storage = Create(client);
        await Assert.ThrowsAsync<IOException>(() => storage.PublishAsync(Key(bytes), bytes, "image/gif", default));
        await client.Received(1).PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task KeyHashMustMatchPublishedBytes_BeforeAnyProviderCall()
    {
        var client = Substitute.For<IAmazonS3>();
        using var storage = Create(client);
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(() => storage.PublishAsync(Key([1]), [2], "image/gif", default));
        Assert.That(client.ReceivedCalls(), Is.Empty);
    }

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
