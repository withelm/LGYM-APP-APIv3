using System.ComponentModel.DataAnnotations;
using LgymApi.Api.Interfaces;

namespace LgymApi.Api.Features.Common.Contracts;

/// <summary>Shared query model for page-based collection endpoints without filtering or sorting.</summary>
public sealed class PaginationQueryVm : IDto
{
    [Range(1, 1000000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}
