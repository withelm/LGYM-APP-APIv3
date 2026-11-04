using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Resources;

namespace LgymApi.Identity.Approval;

internal static class TrainerApprovalPolicy
{
    internal static bool IsApproved(User user) =>
        !user.IsDeleted && !user.IsBlocked && user.TrainerApprovalStatus == TrainerApprovalStatus.Approved;

    internal static List<string> EffectiveRoles(User user, IEnumerable<string> roles) =>
        roles.Where(role => role != AuthConstants.Roles.Trainer || IsApproved(user)).ToList();

    internal static List<string> EffectivePermissions(User user, IEnumerable<string> permissions) =>
        permissions.Where(permission => permission != AuthConstants.Permissions.TrainerAccess || IsApproved(user)).ToList();

    internal static AppError? LoginError(User user) => user.TrainerApprovalStatus switch
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
