using FluentValidation;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Resources;

namespace LgymApi.Api.Features.Trainer.Validation;

public sealed class UpsertReportTemplateRequestValidator : AbstractValidator<UpsertReportTemplateRequest>
{
    public UpsertReportTemplateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(Messages.FieldRequired);

        RuleFor(x => x.Fields)
            .NotEmpty()
            .WithMessage(Messages.FieldRequired);

        RuleForEach(x => x.Fields).ChildRules(fields =>
        {
            fields.RuleFor(f => f.Key)
                .NotEmpty()
                .WithMessage(Messages.FieldRequired);

            fields.RuleFor(f => f.Label)
                .NotEmpty()
                .WithMessage(Messages.FieldRequired);

            fields.RuleFor(f => f.Order)
                .GreaterThanOrEqualTo(0)
                .WithMessage(Messages.FieldRequired);
        });
    }
}
