using FluentValidation;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Trainer.Validation;

public sealed class SubmitReportRequestRequestValidator : AbstractValidator<SubmitReportRequestRequest>
{
    public SubmitReportRequestRequestValidator()
    {
        RuleFor(x => x.Answers)
            .NotNull()
            .WithMessage(Messages.FieldRequired);
    }
}
