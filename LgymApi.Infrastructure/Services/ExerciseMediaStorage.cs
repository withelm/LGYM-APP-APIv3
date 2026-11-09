using LgymApi.Infrastructure.Options;
using System.Net;
using System.Text.RegularExpressions;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;

namespace LgymApi.Infrastructure.Services;

public sealed class ExerciseMediaStorage : IExerciseMediaStorage, IDisposable
{
    private readonly ExerciseMediaStorageOptions _options;
    private readonly IAmazonS3? _client;
    public bool IsConfigured => _options.Enabled;

    public ExerciseMediaStorage(ExerciseMediaStorageOptions options)
    {
        options.Validate();
        _options = options;
        if (options.Enabled) _client = new AmazonS3Client(new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey), new AmazonS3Config
        {
            ServiceURL = options.Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = "auto",
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        });
    }

    private IAmazonS3 Client => _client ?? throw new InvalidOperationException("Exercise media storage is not configured.");
    public Task<string> SignUploadAsync(string key, string contentType, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Client.GetPreSignedURL(new GetPreSignedUrlRequest { BucketName = _options.PrivateBucket, Key = key, Verb = HttpVerb.PUT, ContentType = contentType, Expires = expiresAt.UtcDateTime }));
    }

    public async Task<byte[]> ReadPrivateAsync(string key, CancellationToken cancellationToken)
    {
        using var response = await Client.GetObjectAsync(_options.PrivateBucket, key, cancellationToken);
        return await ReadBoundedAsync(response, cancellationToken);
    }

    private static async Task<byte[]> ReadBoundedAsync(GetObjectResponse response, CancellationToken cancellationToken)
    {
        if (response.ContentLength is <= 0 or > ExerciseMediaLimits.MaxBytes) throw new ExerciseMediaRejectedException();
        using var output = new MemoryStream();
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var read = await response.ResponseStream.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (output.Length + read > ExerciseMediaLimits.MaxBytes) throw new ExerciseMediaRejectedException();
            output.Write(buffer, 0, read);
        }
        if (output.Length != response.ContentLength) throw new ExerciseMediaRejectedException();
        return output.ToArray();
    }

    public Task WriteVerifiedAsync(string key, byte[] bytes, string contentType, CancellationToken cancellationToken) => PutImmutableAsync(_options.PrivateBucket, key, bytes, contentType, false, cancellationToken);

    public async Task<string> PublishAsync(string key, byte[] bytes, string contentType, CancellationToken cancellationToken)
    {
        if (!Regex.IsMatch(key, @"\Aexercises/[0-9a-f-]{36}/[0-9a-f]{64}/(?:animation\.gif|image\.(?:png|jpg|webp))\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100))) throw new ExerciseMediaRejectedException();
        await PutImmutableAsync(_options.PublishedBucket, key, bytes, contentType, true, cancellationToken);
        return _options.PublicBaseUrl.TrimEnd('/') + "/" + key;
    }

    private async Task PutImmutableAsync(string bucket, string key, byte[] bytes, string contentType, bool published, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(bytes, false);
        try
        {
            await Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = stream,
                ContentType = contentType,
                IfNoneMatch = "*",
                // R2 does not implement AWSSDK's streaming SigV4 uploads. Integrity is checked below.
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true,
                UseChunkEncoding = false,
                Headers = { CacheControl = published ? "public,max-age=31536000,immutable" : "private,no-store" }
            }, cancellationToken);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            // A duplicate immutable write is confirmed by the same read-back check as a new write.
        }
        using var existing = await Client.GetObjectAsync(bucket, key, cancellationToken);
        if (existing.ContentLength != bytes.Length) throw new ExerciseMediaRejectedException();
        var storedBytes = await ReadBoundedAsync(existing, cancellationToken);
        if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(System.Security.Cryptography.SHA256.HashData(storedBytes), System.Security.Cryptography.SHA256.HashData(bytes))) throw new ExerciseMediaRejectedException();
    }

    public async Task DeletePrivateAsync(string key, CancellationToken cancellationToken) => await Client.DeleteObjectAsync(_options.PrivateBucket, key, cancellationToken);
    public void Dispose() => _client?.Dispose();
}
