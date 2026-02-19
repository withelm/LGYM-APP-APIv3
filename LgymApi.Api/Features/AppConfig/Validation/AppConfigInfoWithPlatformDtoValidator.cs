using FluentValidation;
using LgymApi.Api.Features.AppConfig.Contracts;
using LgymApi.Domain.Enums;
using LgymApi.Resources;

namespace LgymApi.Api.Features.AppConfig.Validation;

public sealed class AppConfigInfoWithPlatformDtoValidator : AbstractValidator<AppConfigInfoWithPlatformDto>
{
    public AppConfigInfoWithPlatformDtoValidator()
    {
        RuleFor(x => x.Platform)
            .IsInEnum()
            .NotEqual(Platforms.Unknown)
            .WithMessage(Messages.FieldRequired);
    }
}
