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
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            Timeout = TimeSpan.FromSeconds(30)
        });
    }

    private IAmazonS3 Client => _client ?? throw new InvalidOperationException("Exercise media storage is not configured.");
    public string GetPublicUrl(string key)
    {
        if (!IsConfigured) throw new InvalidOperationException("Exercise media storage is not configured.");
        ValidateKey(key);
        return _options.PublicBaseUrl.TrimEnd('/') + "/" + key;
    }

    private static void ValidateKey(string key)
    {
        if (!Regex.IsMatch(key, @"\Aexercises/[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}/[0-9a-f]{64}/(?:animation\.gif|image\.(?:png|jpg|webp))\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100))) throw new ExerciseMediaRejectedException();
    }

    public async Task<string> PublishAsync(string key, byte[] bytes, string contentType, CancellationToken cancellationToken)
    {
        var url = GetPublicUrl(key);
        await PutImmutableAsync(_options.Bucket, key, bytes, contentType, cancellationToken);
        return url;
    }

    private async Task PutImmutableAsync(string bucket, string key, byte[] bytes, string contentType, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(bytes, false);
        var response = await Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            IfNoneMatch = "*",
            // R2 does not implement streaming SigV4 uploads or optional AWS checksums.
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true,
            UseChunkEncoding = false,
            Headers = { CacheControl = "public,max-age=31536000,immutable" }
        }, cancellationToken);
        if (response.HttpStatusCode != HttpStatusCode.OK)
            throw new IOException("Exercise media storage did not confirm the write.");
    }

    public void Dispose() => _client?.Dispose();
}
