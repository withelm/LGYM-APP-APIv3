using FluentValidation;
using LgymApi.Api.Features.AppConfig.Contracts;
using LgymApi.Domain.Enums;
using LgymApi.Resources;

namespace LgymApi.Api.Features.AppConfig.Validation;

public sealed class AppConfigPlatformRequestDtoValidator : AbstractValidator<AppConfigPlatformRequestDto>
{
    public AppConfigPlatformRequestDtoValidator()
    {
        RuleFor(x => x.Platform)
            .NotEqual(Platforms.Unknown)
            .WithMessage(Messages.FieldRequired);
    }
}

public sealed class AppConfigInfoWithPlatformDtoValidator : AbstractValidator<AppConfigInfoWithPlatformDto>
{
    public AppConfigInfoWithPlatformDtoValidator()
    {
        RuleFor(x => x.Platform)
            .NotEqual(Platforms.Unknown)
            .WithMessage(Messages.FieldRequired);
    }
}
