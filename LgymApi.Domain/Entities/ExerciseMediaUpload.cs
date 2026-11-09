using LgymApi.Domain.ValueObjects;
using LgymApi.Domain.ExerciseMedia;

namespace LgymApi.Domain.Entities;

/// <summary>Scoped upload state and durable media-operation receipt, not an exercise-media aggregate.</summary>
public sealed class ExerciseMediaUpload : EntityBase<ExerciseMediaUpload>, IExerciseMediaState
{
    public Id<User> ActorId { get; set; }
    public Id<Exercise>? TargetExerciseId { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string SourceSha256 { get; set; } = string.Empty;
    public string? PublishedSha256 { get; set; }
    public string? Extension { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string Status { get; set; } = "publishing";
    public long Revision { get; set; }
    public Id<ExerciseMediaOperationReference> OperationId { get; set; }
    public string? OperationHash { get; set; }
    public string? PreviousImage { get; set; }
    public string? ResultImage { get; set; }
    public string? ObjectKey { get; set; }
    public int? RejectionStatusCode { get; set; }
}
