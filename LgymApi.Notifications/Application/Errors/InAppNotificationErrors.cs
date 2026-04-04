using LgymApi.Application.Common.Errors;
using LgymApi.Resources;

namespace LgymApi.Notifications.Application.Errors;

public sealed class InAppNotificationNotFoundError : NotFoundError
{
    public InAppNotificationNotFoundError() : base(Messages.InAppNotificationNotFound) { }
}

public sealed class InAppNotificationForbiddenError : ForbiddenError
{
    public InAppNotificationForbiddenError() : base(Messages.InAppNotificationForbidden) { }
}
