using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using LgymApi.Api.Interfaces;
using LgymApi.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LgymApi.Api.Features.Exercise.Contracts;

public sealed class CreateExerciseMediaDto : IDto
{
    [FromForm(Name = "operationId")] public string OperationId { get; set; } = string.Empty;
    [FromForm(Name = "name"), MaxLength(200)] public string Name { get; set; } = string.Empty;
    [FromForm(Name = "bodyPart"), BindRequired] public BodyParts BodyPart { get; set; }
    [FromForm(Name = "eloFormulaId")] public string? EloFormulaId { get; set; }
    [FromForm(Name = "description"), MaxLength(16000)] public string? Description { get; set; }
    [FromForm(Name = "sourceSha256")] public string? SourceSha256 { get; set; }
    [FromForm(Name = "contentType")] public string? ContentType { get; set; }
    [FromForm(Name = "sizeBytes")] public long? SizeBytes { get; set; }
    [FromForm(Name = "file")] public IFormFile? File { get; set; }
}

public sealed class ChangeExerciseMediaDto : IDto
{
    [FromForm(Name = "operationId")] public string OperationId { get; set; } = string.Empty;
    /// <summary>Required form field. An empty value explicitly means expected null.</summary>
    [FromForm(Name = "expectedImage"), BindRequired] public string? ExpectedImage { get; set; }
    [FromForm(Name = "sourceSha256")] public string? SourceSha256 { get; set; }
    [FromForm(Name = "contentType")] public string? ContentType { get; set; }
    [FromForm(Name = "sizeBytes")] public long? SizeBytes { get; set; }
    [FromForm(Name = "file")] public IFormFile? File { get; set; }
    [FromForm(Name = "remove")] public bool Remove { get; set; }
    [FromForm(Name = "restoreImage")] public string? RestoreImage { get; set; }
}

public sealed class ExerciseMediaMutationDto : IResultDto
{
    [JsonPropertyName("_id")] public string Id { get; set; } = string.Empty;
    public string? Image { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class ExerciseMediaErrorDto : IResultDto
{
    [JsonPropertyName("msg")] public string Message { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public string Disposition { get; set; } = string.Empty;
}

public sealed class ExerciseMediaCleanupDto : IResultDto
{
    public int Cleaned { get; set; }
    public int Remaining { get; set; }
}
