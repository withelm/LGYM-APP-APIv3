using FluentValidation;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Trainer.Validation;

public sealed class UpdateOwnDietPlanRequestValidator : AbstractValidator<UpdateOwnDietPlanRequest>
{
    public UpdateOwnDietPlanRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .WithMessage(Messages.NameIsRequired)
            .MaximumLength(120)
            .WithMessage(Messages.MaximumLengthExceeded);
        RuleFor(request => request.Notes)
            .MaximumLength(2000)
            .WithMessage(Messages.MaximumLengthExceeded);
        RuleFor(request => request.EndDate)
            .GreaterThanOrEqualTo(request => request.StartDate)
            .WithMessage(Messages.InvalidDateRange)
            .When(request => request.EndDate.HasValue);
        RuleFor(request => request.Meals)
            .NotNull()
            .WithMessage(Messages.FieldRequired);
        RuleForEach(request => request.Meals).SetValidator(new UpdateOwnDietMealRequestValidator());
    }
}

public sealed class UpdateOwnDietMealRequestValidator : AbstractValidator<UpsertDietMealRequest>
{
    public UpdateOwnDietMealRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .WithMessage(Messages.NameIsRequired)
            .MaximumLength(160)
            .WithMessage(Messages.MaximumLengthExceeded);
        RuleFor(request => request.Description)
            .MaximumLength(2000)
            .WithMessage(Messages.MaximumLengthExceeded);
        RuleFor(request => request.Order)
            .GreaterThanOrEqualTo(0)
            .WithMessage(Messages.ValueMustBeNonNegative);
    }
}
