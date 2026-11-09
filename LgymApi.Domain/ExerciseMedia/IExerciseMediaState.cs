using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.ExerciseMedia;

/// <summary>Workout-owned ledger state shared by the persisted upload and its application projection.</summary>
public interface IExerciseMediaState
{
    Id<Exercise>? TargetExerciseId { get; set; }
    string ContentType { get; set; }
    long SizeBytes { get; set; }
    string SourceSha256 { get; set; }
    string? PublishedSha256 { get; set; }
    string? Extension { get; set; }
    int Width { get; set; }
    int Height { get; set; }
    int Frames { get; set; }
    DateTimeOffset ExpiresAt { get; set; }
    string Status { get; set; }
    long Revision { get; set; }
    Id<ExerciseMediaOperationReference>? OperationId { get; set; }
    string? OperationHash { get; set; }
    string? PreviousImage { get; set; }
    string? ResultImage { get; set; }
    bool PrivateObjectsCleaned { get; set; }
}
