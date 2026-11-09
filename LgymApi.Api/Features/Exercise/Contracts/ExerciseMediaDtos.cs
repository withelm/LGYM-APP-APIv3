using System.Text.Json.Serialization;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Interfaces;
using LgymApi.Domain.Enums;

namespace LgymApi.Api.Features.Exercise.Contracts;

public sealed class InitiateExerciseMediaDto : IDto
{
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string? ExerciseId { get; set; }
}

public sealed class CreateExerciseMediaDto : IDto
{
    public string OperationId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    [JsonRequired] public BodyParts BodyPart { get; set; }
    public LookupItemVm? EloFormula { get; set; }
    public string? Description { get; set; }
    public string? UploadReference { get; set; }
}

public sealed class ChangeExerciseMediaDto : IDto
{
    public string OperationId { get; set; } = string.Empty;
    /// <summary>Required, including explicit null when adding the first asset.</summary>
    [JsonRequired] public string? ExpectedImage { get; set; }
    public string? UploadReference { get; set; }
    public bool Remove { get; set; }
    /// <summary>Conditional rollback to a previously recorded URL, never an arbitrary URL.</summary>
    public string? RestoreImage { get; set; }
}

public sealed class ExerciseMediaUploadDto : IResultDto
{
    public string UploadReference { get; set; } = string.Empty;
    public string UploadUrl { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public Dictionary<string, string> Headers { get; set; } = [];
}

public sealed class ExerciseMediaVerificationDto : IResultDto
{
    public string UploadReference { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string SourceSha256 { get; set; } = string.Empty;
    public string PublishedSha256 { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public int Frames { get; set; }
}

public sealed class ExerciseMediaMutationDto : IResultDto
{
    [JsonPropertyName("_id")] public string Id { get; set; } = string.Empty;
    public string? Image { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class ExerciseMediaCleanupDto : IResultDto
{
    public int Cleaned { get; set; }
    public int Remaining { get; set; }
}
