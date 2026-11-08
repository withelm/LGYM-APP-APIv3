using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Resources;

namespace LgymApi.Identity.Approval;

internal static class TrainerApprovalPolicy
{
    internal static bool IsApproved(User user, TrainerApprovalStatus? status) =>
        !user.IsDeleted && !user.IsBlocked && status == TrainerApprovalStatus.Approved;

    internal static List<string> EffectiveRoles(List<string> roles, bool isApproved) =>
        isApproved ? roles : roles.Where(role => role != AuthConstants.Roles.Trainer).ToList();

    internal static List<string> EffectivePermissions(List<string> permissions, bool isApproved) =>
        isApproved ? permissions : permissions.Where(permission => permission != AuthConstants.Permissions.TrainerAccess).ToList();

    internal static TrainerApprovalError? LoginError(TrainerApprovalStatus? status) => status switch
    {
        TrainerApprovalStatus.PendingApproval => new TrainerApprovalError(
            "TrainerPendingApproval", Messages.TrainerPendingApproval, 403),
        TrainerApprovalStatus.Rejected => new TrainerApprovalError(
            "TrainerRejected", Messages.TrainerRejected, 403),
        _ => null
    };
}

internal sealed class TrainerApprovalError(string code, string message, int statusCode) : AppError
{
    public override string Message => message;
    public override int HttpStatusCode => statusCode;
    public override object GetPayload() => new { msg = Message, code };
}
