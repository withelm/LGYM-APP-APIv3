using FluentValidation;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.User.Validation;

public sealed class RegisterPushInstallationRequestValidator : AbstractValidator<RegisterPushInstallationRequest>
{
    public RegisterPushInstallationRequestValidator()
    {
        RuleFor(x => x.InstallationId)
            .NotEmpty()
            .WithMessage(Messages.FieldRequired);

        RuleFor(x => x.Platform)
            .NotEmpty()
            .WithMessage(Messages.FieldRequired);

        RuleFor(x => x.FcmToken)
            .NotEmpty()
            .WithMessage(Messages.FieldRequired);

        RuleFor(x => x.Environment)
            .NotEmpty()
            .WithMessage(Messages.FieldRequired);
    }
}

public sealed class PushInstallationActionRequestValidator : AbstractValidator<PushInstallationActionRequest>
{
    public PushInstallationActionRequestValidator()
    {
        RuleFor(x => x.InstallationId)
            .NotEmpty()
            .WithMessage(Messages.FieldRequired);
    }
}
