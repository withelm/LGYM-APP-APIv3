using FluentValidation;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Trainer.Validation;

public sealed class UpsertRecurringReportAssignmentRequestValidator : AbstractValidator<UpsertRecurringReportAssignmentRequest>
{
    public UpsertRecurringReportAssignmentRequestValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty()
            .WithMessage(Messages.FieldRequired)
            .Must(id => Id<LgymApi.Domain.Entities.ReportTemplate>.TryParse(id, out _))
            .WithMessage(Messages.FieldRequired);

        RuleFor(x => x.IntervalValue)
            .GreaterThan(0)
            .WithMessage(Messages.FieldRequired);

        RuleFor(x => x.StartsAt)
            .NotEmpty()
            .WithMessage(Messages.FieldRequired);

        RuleFor(x => x)
            .Must(x => !x.EndsAt.HasValue || x.EndsAt.Value >= x.StartsAt)
            .WithMessage(Messages.InvalidDateRange);
    }
}
