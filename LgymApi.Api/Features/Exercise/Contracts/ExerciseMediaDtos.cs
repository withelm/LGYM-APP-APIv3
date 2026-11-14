using System.ComponentModel.DataAnnotations;
using LgymApi.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Exercise.Contracts;

public sealed class ExerciseMediaUploadDto : IDto
{
    [FromForm(Name = "file")]
    [Required]
    public IFormFile File { get; set; } = null!;
}

public sealed class ExerciseMediaUploadResponseDto : IResultDto
{
    public string Image { get; set; } = string.Empty;
}
