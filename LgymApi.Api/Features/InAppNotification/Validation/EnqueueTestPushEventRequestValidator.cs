using FluentValidation;
using LgymApi.Api.Features.InAppNotification.Contracts;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using InAppNotificationEntity = global::LgymApi.Domain.Entities.InAppNotification;
using UserEntity = global::LgymApi.Domain.Entities.User;

namespace LgymApi.Api.Features.InAppNotification.Validation;

public sealed class EnqueueTestPushEventRequestValidator : AbstractValidator<EnqueueTestPushEventRequest>
{
    public EnqueueTestPushEventRequestValidator()
    {
        RuleFor(x => x.RecipientUserId)
            .NotEmpty()
            .WithMessage(Messages.FieldRequired)
            .Must(static raw => Id<UserEntity>.TryParse(raw, out _))
            .WithMessage(Messages.UserIdRequired);

        RuleFor(x => x.Type)
            .NotEmpty()
            .WithMessage(Messages.FieldRequired);

        RuleFor(x => x.EventId)
            .NotEmpty()
            .WithMessage(Messages.FieldRequired);

        RuleFor(x => x.InAppNotificationId)
            .Must(static raw => string.IsNullOrWhiteSpace(raw) || Id<InAppNotificationEntity>.TryParse(raw, out _))
            .WithMessage(Messages.FieldRequired);
    }
}
