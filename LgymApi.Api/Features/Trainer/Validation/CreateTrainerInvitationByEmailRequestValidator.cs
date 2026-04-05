using FluentValidation;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Trainer.Validation;

public sealed class CreateTrainerInvitationByEmailRequestValidator : AbstractValidator<CreateTrainerInvitationByEmailRequest>
{
    public CreateTrainerInvitationByEmailRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage(Messages.FieldRequired)
            .EmailAddress()
            .WithMessage(Messages.FieldRequired);

        RuleFor(x => x.PreferredLanguage)
            .Length(2, 10)
            .When(x => !string.IsNullOrEmpty(x.PreferredLanguage));

        RuleFor(x => x.PreferredTimeZone)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.PreferredTimeZone));
    }
}
