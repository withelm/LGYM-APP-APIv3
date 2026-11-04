using LgymApi.Api.Interfaces;

namespace LgymApi.Api.Features.Common.Contracts;

public sealed class TrainerApprovalErrorDto : IResultDto
{
    public string Msg { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}
