using FluentValidation;
using LgymApi.Api.DTOs;
using LgymApi.Resources;

namespace LgymApi.Api.Plans.Validation;

public class CopyPlanDtoValidator : AbstractValidator<CopyPlanDto>
{
    public CopyPlanDtoValidator()
    {
        RuleFor(x => x.ShareCode)
            .NotEmpty()
            .WithMessage(Messages.ShareCodeRequired);
    }
}
