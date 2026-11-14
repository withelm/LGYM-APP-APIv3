using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using LgymApi.Application.Options;
using LgymApi.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

var checks = new List<string>();
var stage = "arguments";
string? errorType = null;
string? receiptPath = args.Length == 4 && args[0] == "run" ? args[3] : null;
var passed = false;
try
{
    if (args is ["prepare-tls", var directory])
    {
        stage = "prepare-tls";
        Directory.CreateDirectory(directory);
        using var keyPair = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", keyPair, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(now, now.AddHours(1));
        File.WriteAllText(Path.Combine(directory, "certificate.pem"), certificate.ExportCertificatePem());
        File.WriteAllText(Path.Combine(directory, "key.pem"), keyPair.ExportPkcs8PrivateKeyPem());
        Console.WriteLine("PASS prepare-tls");
        return 0;
    }

    if (args is not ["run", var endpoint, var certificatePath, _])
        throw new ArgumentException();
    if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) ||
        endpointUri.Scheme != Uri.UriSchemeHttps || !endpointUri.IsLoopback ||
        !string.IsNullOrEmpty(endpointUri.UserInfo) || endpointUri.AbsolutePath != "/" ||
        !string.IsNullOrEmpty(endpointUri.Query) || !string.IsNullOrEmpty(endpointUri.Fragment))
        throw new ArgumentException();
    var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
    var secret = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
    if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secret))
        throw new InvalidOperationException();

    stage = "certificate";
    using var trustedCertificate = X509CertificateLoader.LoadCertificateFromFile(certificatePath);
    var factory = new PinnedLoopbackHttpClientFactory(trustedCertificate.GetCertHashString(HashAlgorithmName.SHA256));
    var originalFactory = Amazon.AWSConfigs.HttpClientFactory;
    try
    {
        Amazon.AWSConfigs.HttpClientFactory = factory;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = timeout.Token;
        var bucket = "lgym-smoke-" + RandomNumberGenerator.GetHexString(24).ToLowerInvariant();
        var key = "photos/" + RandomNumberGenerator.GetHexString(24).ToLowerInvariant() + "/smoke.jpg";
        byte[] bytes = [0xff, 0xd8, 0xff, 0xd9];
        using var client = new AmazonS3Client(new BasicAWSCredentials(accessKey, secret), new AmazonS3Config
        {
            ServiceURL = endpoint, ForcePathStyle = true, AuthenticationRegion = "auto"
        });
        using var http = factory.CreateHttpClient(new AmazonS3Config());
        var provider = new CloudflareR2PhotoStorageProvider(new PhotoStorageOptions
        {
            Endpoint = endpoint, BucketName = bucket, AccessKeyId = accessKey, SecretAccessKey = secret
        }, NullLogger<CloudflareR2PhotoStorageProvider>.Instance);

        stage = "create-bucket";
        await client.PutBucketAsync(new PutBucketRequest { BucketName = bucket }, token);
        Complete();
        stage = "signed-put";
        var putUrl = await provider.GenerateSignedUploadUrlAsync(key, "image/jpeg", TimeSpan.FromMinutes(10), token);
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new("image/jpeg");
        using var put = await http.PutAsync(putUrl, content, token);
        put.EnsureSuccessStatusCode();
        Complete();
        stage = "signed-get";
        var readUrl = await provider.GenerateSignedReadUrlAsync(key, TimeSpan.FromMinutes(15), token);
        using var before = await http.GetAsync(readUrl, token);
        before.EnsureSuccessStatusCode();
        if (!(await before.Content.ReadAsByteArrayAsync(token)).AsSpan().SequenceEqual(bytes))
            throw new InvalidOperationException();
        Complete();
        stage = "prefix-list";
        var page = await provider.ListAsync("photos/", null, token);
        if (page.Keys.Count != 1 || page.Keys[0] != key || page.ContinuationToken is not null)
            throw new InvalidOperationException();
        Complete();
        stage = "delete-and-old-get-404";
        await provider.DeleteAsync(key, token);
        using var after = await http.GetAsync(readUrl, token);
        if (after.StatusCode != HttpStatusCode.NotFound)
            throw new InvalidOperationException();
        Complete();
        stage = "repeated-delete";
        await provider.DeleteAsync(key, token);
        Complete();
        stage = "metadata-null";
        if (await provider.GetMetadataAsync(key, token) is not null)
            throw new InvalidOperationException();
        Complete();
        stage = "delete-bucket";
        await client.DeleteBucketAsync(bucket, token);
        Complete();
        passed = checks.Count == 8;
    }
    finally
    {
        Amazon.AWSConfigs.HttpClientFactory = originalFactory;
    }
}
catch (Exception exception)
{
    errorType = exception.GetType().Name;
    Console.Error.WriteLine($"FAIL {stage} {errorType}");
}

if (receiptPath is not null)
{
    try
    {
        File.WriteAllText(receiptPath, JsonSerializer.Serialize(new
        {
            passed, expectedChecks = 8, checks, failedStage = passed ? null : stage,
            errorType, timestampUtc = DateTimeOffset.UtcNow
        }));
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"FAIL receipt {exception.GetType().Name}");
        return 1;
    }
}
return passed ? 0 : 1;

void Complete()
{
    checks.Add(stage);
    Console.WriteLine($"PASS {stage}");
}

sealed class PinnedLoopbackHttpClientFactory(string expectedCertificateHash) : Amazon.Runtime.HttpClientFactory
{
    public override HttpClient CreateHttpClient(IClientConfig clientConfig)
        => new(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            ServerCertificateCustomValidationCallback = (request, certificate, _, errors) =>
                request.RequestUri is { IsLoopback: true, Scheme: "https" } && certificate is not null
                && (errors & (SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateNotAvailable)) == 0
                && certificate.GetCertHashString(HashAlgorithmName.SHA256) == expectedCertificateHash
                && DateTime.UtcNow >= certificate.NotBefore.ToUniversalTime()
                && DateTime.UtcNow <= certificate.NotAfter.ToUniversalTime()
        }) { Timeout = TimeSpan.FromSeconds(60) };
}
