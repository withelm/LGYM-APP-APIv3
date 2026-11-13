using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using LgymApi.Infrastructure.Options;
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
        url.Should().Be("https://exercises-dev.owner.test/" + Key(bytes, filename));
        uploaded.Should().Equal(bytes);
        captured!.BucketName.Should().Be(Bucket);
        captured.Key.Should().Be(Key(bytes, filename));
        captured.ContentType.Should().Be(contentType);
        captured.IfNoneMatch.Should().Be("*");
        captured.DisablePayloadSigning.Should().BeTrue();
        captured.DisableDefaultChecksumValidation.Should().BeTrue();
        captured.UseChunkEncoding.Should().BeFalse();
        captured.Headers.CacheControl.Should().Be("public,max-age=31536000,immutable");
        client.ReceivedCalls().Should().OnlyContain(call => call.GetMethodInfo().Name == "PutObjectAsync");
    }

    [TestCase(HttpStatusCode.PreconditionFailed)]
    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    public async Task Publish_PropagatesStorageFailureWithoutReadbackOrSuccess(HttpStatusCode status)
    {
        var client = Substitute.For<IAmazonS3>();
        client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<PutObjectResponse>(new AmazonS3Exception("synthetic failure") { StatusCode = status }));
        using var storage = Create(client);
        await Assert.ThrowsAsync<AmazonS3Exception>(async () => await storage.PublishAsync(Key([1]), [1], "image/gif", default));
        client.ReceivedCalls().Should().OnlyContain(call => call.GetMethodInfo().Name == "PutObjectAsync");
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
        await act.Should().ThrowAsync<OperationCanceledException>();
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
