using LgymApi.Api.Interfaces;
using LgymApi.Domain.Enums;

namespace LgymApi.Api.Features.AdminManagement.Contracts;

public sealed class TrainerApplicationDto : IResultDto
{
    public string AccountId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public TrainerApprovalStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int Attempt { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
}

public sealed class PaginatedTrainerApplicationsDto : IResultDto
{
    public List<TrainerApplicationDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
