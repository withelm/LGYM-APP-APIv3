using System.Net;
using System.Reflection;
using Amazon.S3;
using Amazon.S3.Model;
using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Options;
using LgymApi.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class CloudflareR2PhotoStorageProviderTests
{
    [Test]
    public async Task GenerateSignedUploadUrlAsync_UsesPutRequestAndReturnsUrl()
    {
        var client = Substitute.For<IAmazonS3>();
        GetPreSignedUrlRequest? captured = null;
        client.GetPreSignedURL(Arg.Do<GetPreSignedUrlRequest>(request => captured = request)).Returns("https://upload.example.com");
        var provider = CreateProvider(client);

        var result = await provider.GenerateSignedUploadUrlAsync("photos/key.jpg", "image/jpeg", TimeSpan.FromMinutes(10));

        Assert.That(result, Is.EqualTo("https://upload.example.com"));
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Verb, Is.EqualTo(HttpVerb.PUT));
        Assert.That(captured.ContentType, Is.EqualTo("image/jpeg"));
        Assert.That(captured.Key, Is.EqualTo("photos/key.jpg"));
    }

    [Test]
    public async Task GenerateSignedReadUrlAsync_UsesGetRequestAndReturnsUrl()
    {
        var client = Substitute.For<IAmazonS3>();
        GetPreSignedUrlRequest? captured = null;
        client.GetPreSignedURL(Arg.Do<GetPreSignedUrlRequest>(request => captured = request)).Returns("https://read.example.com");
        var provider = CreateProvider(client);

        var result = await provider.GenerateSignedReadUrlAsync("photos/key.jpg", TimeSpan.FromMinutes(5));

        Assert.That(result, Is.EqualTo("https://read.example.com"));
        Assert.That(captured!.Verb, Is.EqualTo(HttpVerb.GET));
    }

    [Test]
    public async Task DeleteAsync_WhenObjectMissing_DoesNotThrow()
    {
        var client = Substitute.For<IAmazonS3>();
        client.DeleteObjectAsync(Arg.Any<DeleteObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<DeleteObjectResponse>>(_ => throw new AmazonS3Exception("missing") { StatusCode = HttpStatusCode.NotFound });
        var provider = CreateProvider(client);

        var action = async () => await provider.DeleteAsync("photos/key.jpg");

        await Assert.ThatAsync(async () => { await action(); }, Throws.Nothing);
    }

    [Test]
    public async Task GetMetadataAsync_WhenObjectExists_MapsResponse()
    {
        var client = Substitute.For<IAmazonS3>();
        client.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetObjectMetadataResponse
            {
                Headers = { ContentLength = 1234, ContentType = "image/png" },
                ETag = "\"etag\"",
                LastModified = new DateTime(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc)
            });
        var provider = CreateProvider(client);

        var result = await provider.GetMetadataAsync("photos/key.png");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.SizeBytes, Is.EqualTo(1234));
        Assert.That(result.ContentType, Is.EqualTo("image/png"));
        Assert.That(result.ETag, Is.EqualTo("etag"));
    }

    [Test]
    public async Task GetMetadataAsync_WhenObjectMissing_ReturnsNull()
    {
        var client = Substitute.For<IAmazonS3>();
        client.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<GetObjectMetadataResponse>>(_ => throw new AmazonS3Exception("missing") { StatusCode = HttpStatusCode.NotFound });
        var provider = CreateProvider(client);

        var result = await provider.GetMetadataAsync("photos/missing.png");

        Assert.That(result, Is.Null);
    }

    private static CloudflareR2PhotoStorageProvider CreateProvider(IAmazonS3 client)
    {
        var provider = new CloudflareR2PhotoStorageProvider(
            new PhotoStorageOptions
            {
                Endpoint = "https://example.r2.cloudflarestorage.com",
                BucketName = "bucket",
                AccessKeyId = "access",
                SecretAccessKey = "secret"
            },
            Substitute.For<ILogger<CloudflareR2PhotoStorageProvider>>());

        typeof(CloudflareR2PhotoStorageProvider)
            .GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(provider, client);

        return provider;
    }
}
