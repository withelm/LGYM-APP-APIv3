using FluentValidation;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Trainer.Validation;

public sealed class CreateTrainerInvitationRequestValidator : AbstractValidator<CreateTrainerInvitationRequest>
{
    public CreateTrainerInvitationRequestValidator()
    {
        RuleFor(x => x.TraineeId)
            .NotEmpty()
            .WithMessage(Messages.UserIdRequired)
            .Must(id => Id<LgymApi.Domain.Entities.User>.TryParse(id, out _))
            .WithMessage(Messages.UserIdRequired);
    }
}
