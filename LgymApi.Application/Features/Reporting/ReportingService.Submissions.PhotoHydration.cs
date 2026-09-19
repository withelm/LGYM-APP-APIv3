using System.Text.Json;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.Reporting;

public sealed partial class ReportingService
{
    private async Task HydrateSubmissionPhotosAsync(
        IReadOnlyList<ReportSubmissionResult> submissions,
        CancellationToken cancellationToken)
    {
        var hydratableSubmissions = submissions
            .Select(submission => (Submission: submission, AnswerKeys: ResolvePhotoEnvelopeAnswerKeys(submission)))
            .Where(candidate => candidate.AnswerKeys.Count > 0)
            .ToList();
        if (hydratableSubmissions.Count == 0)
        {
            return;
        }

        var requestIds = hydratableSubmissions
            .Select(candidate => candidate.Submission.ReportRequestId)
            .Distinct()
            .ToArray();
        var canonicalPhotos = await _photoPersistence.ListByRequestsAsync(requestIds, cancellationToken);
        var photosByRequest = canonicalPhotos
            .GroupBy(photo => photo.ReportRequestId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var capabilities = new Dictionary<Id<Photo>, ReportSubmissionPhotoCapabilityResult>();

        foreach (var (submission, answerKeys) in hydratableSubmissions)
        {
            photosByRequest.TryGetValue(submission.ReportRequestId, out var requestPhotos);
            var ownedPhotos = (requestPhotos ?? [])
                .Where(photo => photo.OwnerAccountId == submission.TraineeId)
                .ToList();
            foreach (var answerKey in answerKeys)
            {
                var answer = submission.Answers[answerKey];
                submission.Answers[answerKey] = answer.ValueKind == JsonValueKind.Array
                    ? await HydratePhotoArrayAsync(answer, ownedPhotos, capabilities, cancellationToken)
                    : await HydratePhotoObjectAsync(answer, ownedPhotos, capabilities, cancellationToken);
            }
        }
    }

    private async Task<JsonElement> HydratePhotoArrayAsync(
        JsonElement answer,
        IReadOnlyList<ReportPhotoPersistenceModel> requestPhotos,
        Dictionary<Id<Photo>, ReportSubmissionPhotoCapabilityResult> capabilities,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var item in answer.EnumerateArray())
            {
                if (IsPhotoEnvelope(item))
                {
                    WriteHydratedPhotoEnvelope(
                        writer,
                        item,
                        await ResolvePhotoCapabilityAsync(item, requestPhotos, capabilities, cancellationToken));
                }
                else
                {
                    item.WriteTo(writer);
                }
            }

            writer.WriteEndArray();
            writer.Flush();
        }

        return ParseJsonElement(stream);
    }

    private async Task<JsonElement> HydratePhotoObjectAsync(
        JsonElement answer,
        IReadOnlyList<ReportPhotoPersistenceModel> requestPhotos,
        Dictionary<Id<Photo>, ReportSubmissionPhotoCapabilityResult> capabilities,
        CancellationToken cancellationToken)
    {
        var capability = await ResolvePhotoCapabilityAsync(answer, requestPhotos, capabilities, cancellationToken);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteHydratedPhotoEnvelope(writer, answer, capability);
            writer.Flush();
        }

        return ParseJsonElement(stream);
    }

    private async Task<ReportSubmissionPhotoCapabilityResult> ResolvePhotoCapabilityAsync(
        JsonElement photoEnvelope,
        IReadOnlyList<ReportPhotoPersistenceModel> requestPhotos,
        Dictionary<Id<Photo>, ReportSubmissionPhotoCapabilityResult> capabilities,
        CancellationToken cancellationToken)
    {
        var canonicalPhoto = ResolveCanonicalPhoto(photoEnvelope, requestPhotos);
        if (canonicalPhoto == null)
        {
            return MapPhotoCapability(new ReportPhotoCapabilitySource(null, null, null));
        }

        if (capabilities.TryGetValue(canonicalPhoto.Id, out var cachedCapability))
        {
            return cachedCapability;
        }

        var readUrl = await _photoStorageProvider.GenerateSignedReadUrlAsync(
            canonicalPhoto.StorageKey,
            GetSignedReadExpiration(),
            cancellationToken);
        string? thumbnailUrl = null;
        if (!string.IsNullOrWhiteSpace(canonicalPhoto.ThumbnailStorageKey))
        {
            thumbnailUrl = await _photoStorageProvider.GenerateSignedReadUrlAsync(
                canonicalPhoto.ThumbnailStorageKey,
                GetSignedReadExpiration(),
                cancellationToken);
        }

        var capability = MapPhotoCapability(new ReportPhotoCapabilitySource(canonicalPhoto, readUrl, thumbnailUrl));
        capabilities.Add(canonicalPhoto.Id, capability);
        return capability;
    }

    private ReportSubmissionPhotoCapabilityResult MapPhotoCapability(ReportPhotoCapabilitySource source)
        => _mapper.Map<ReportPhotoCapabilitySource, ReportSubmissionPhotoCapabilityResult>(source);

    private static void WriteHydratedPhotoEnvelope(
        Utf8JsonWriter writer,
        JsonElement photoEnvelope,
        ReportSubmissionPhotoCapabilityResult capability)
    {
        var overwrittenProperties = capability.StorageKey == null ? UrlOutputPropertyNames : CanonicalOutputPropertyNames;
        writer.WriteStartObject();
        foreach (var property in photoEnvelope.EnumerateObject())
        {
            if (!overwrittenProperties.Contains(property.Name))
            {
                property.WriteTo(writer);
            }
        }

        if (capability.StorageKey != null)
        {
            writer.WriteString("storageKey", capability.StorageKey);
        }

        WriteNullableString(writer, "readUrl", capability.ReadUrl);
        WriteNullableString(writer, "thumbnailUrl", capability.ThumbnailUrl);
        writer.WriteEndObject();
    }

    private static void WriteNullableString(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (value == null)
        {
            writer.WriteNull(propertyName);
        }
        else
        {
            writer.WriteString(propertyName, value);
        }
    }

    private static ReportPhotoPersistenceModel? ResolveCanonicalPhoto(
        JsonElement photoEnvelope,
        IReadOnlyList<ReportPhotoPersistenceModel> requestPhotos)
    {
        if (!TryGetUniqueStringProperty(photoEnvelope, "photoId", out var hasPhotoId, out var photoIdValue)
            || !TryGetUniqueStringProperty(photoEnvelope, "_id", out var hasLegacyId, out var legacyIdValue))
        {
            return null;
        }

        var photoId = ParsePhotoId(photoIdValue);
        var legacyId = ParsePhotoId(legacyIdValue);
        if ((hasPhotoId && !photoId.HasValue)
            || (hasLegacyId && !legacyId.HasValue)
            || (photoId.HasValue && legacyId.HasValue && photoId.Value != legacyId.Value))
        {
            return null;
        }

        var canonicalId = photoId ?? legacyId;
        if (canonicalId.HasValue)
        {
            return requestPhotos.FirstOrDefault(photo => photo.Id == canonicalId.Value);
        }

        if (!TryGetUniqueStringProperty(photoEnvelope, "storageKey", out var hasStorageKey, out var storageKey))
        {
            return null;
        }

        return !hasStorageKey || string.IsNullOrWhiteSpace(storageKey)
            ? null
            : requestPhotos.FirstOrDefault(photo => string.Equals(photo.StorageKey, storageKey, StringComparison.Ordinal));
    }

    private static bool TryGetUniqueStringProperty(
        JsonElement photoEnvelope,
        string propertyName,
        out bool isPresent,
        out string? propertyValue)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        isPresent = false;
        propertyValue = null;
        foreach (var property in photoEnvelope.EnumerateObject()
                     .Where(property => string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)))
        {
            isPresent = true;
            if (property.Value.ValueKind != JsonValueKind.String || property.Value.GetString() is not { } value)
            {
                return false;
            }

            values.Add(value);
        }

        if (!isPresent)
        {
            return true;
        }

        if (values.Count != 1)
        {
            return false;
        }

        propertyValue = values.Single();
        return true;
    }

    private static JsonElement ParseJsonElement(MemoryStream stream)
    {
        stream.Position = 0;
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }

    private static List<string> ResolvePhotoEnvelopeAnswerKeys(ReportSubmissionResult submission)
        => submission.Request.Template.Fields
            .Where(field => field.Type == ReportFieldType.Photos)
            .Select(field => field.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(key => submission.Answers.TryGetValue(key, out var answer) && ContainsPhotoEnvelope(answer))
            .ToList();

    private static bool ContainsPhotoEnvelope(JsonElement answer)
        => answer.ValueKind switch
        {
            JsonValueKind.Object => IsPhotoEnvelope(answer),
            JsonValueKind.Array => answer.EnumerateArray().Any(IsPhotoEnvelope),
            _ => false
        };

    private static bool IsPhotoEnvelope(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var declaresCapabilitySlot = false;
        var declaresPhotoIdentifier = false;
        foreach (var property in item.EnumerateObject())
        {
            if (UrlOutputPropertyNames.Contains(property.Name))
            {
                declaresCapabilitySlot |= property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Null;
            }
            else if (PhotoIdentifierPropertyNames.Contains(property.Name))
            {
                declaresPhotoIdentifier |= property.Value.ValueKind == JsonValueKind.String
                    && ParsePhotoId(property.Value.GetString()).HasValue;
            }
        }

        return declaresCapabilitySlot || declaresPhotoIdentifier;
    }

    private static Id<Photo>? ParsePhotoId(string? value)
        => !string.IsNullOrWhiteSpace(value) && Id<Photo>.TryParse(value, out var photoId) ? photoId : null;

    private static readonly HashSet<string> PhotoIdentifierPropertyNames = new(["photoId", "_id"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> UrlOutputPropertyNames = new(["readUrl", "thumbnailUrl"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> CanonicalOutputPropertyNames = new(["storageKey", "readUrl", "thumbnailUrl"], StringComparer.OrdinalIgnoreCase);
}
