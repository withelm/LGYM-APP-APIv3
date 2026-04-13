using FluentValidation;
using LgymApi.Api.Features.AppConfig.Contracts;

namespace LgymApi.Api.Features.AppConfig.Validation;

public sealed class UpdateAppConfigRequestValidator : AbstractValidator<UpdateAppConfigRequest>
{
    public UpdateAppConfigRequestValidator()
    {
        RuleFor(x => x.Platform)
            .IsInEnum()
            .NotEqual(Platforms.Unknown)
            .WithMessage(Messages.FieldRequired);
    }
}
