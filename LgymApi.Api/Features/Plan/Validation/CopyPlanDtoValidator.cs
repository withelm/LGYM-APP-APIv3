using FluentValidation;
using LgymApi.Api.Features.Plan.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Plan.Validation;

public class CopyPlanDtoValidator : AbstractValidator<CopyPlanDto>
{
    public CopyPlanDtoValidator()
    {
        RuleFor(x => x.ShareCode)
            .NotEmpty()
            .WithMessage(Messages.ShareCodeRequired);
    }
}
