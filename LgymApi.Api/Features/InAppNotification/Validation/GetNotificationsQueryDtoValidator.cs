using FluentValidation;
using LgymApi.Api.Features.InAppNotification.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.InAppNotification.Validation;

public sealed class GetNotificationsQueryDtoValidator : AbstractValidator<GetNotificationsQueryDto>
{
    public GetNotificationsQueryDtoValidator()
    {
        RuleFor(x => x.Limit)
            .GreaterThanOrEqualTo(1).WithMessage(Messages.InAppNotificationLimitOutOfRange)
            .LessThanOrEqualTo(50).WithMessage(Messages.InAppNotificationLimitOutOfRange);
    }
}
