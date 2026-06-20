using FluentValidation;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Domain.Enums;
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
                .GreaterThanOrEqualTo(0);

            // Validate module config based on field type
            fields.RuleFor(f => f.ModuleConfig)
                .Must((field, config) => ValidateModuleConfig(field.Type, config))
                .WithMessage("Invalid module configuration for field type");
        });
    }

    private static bool ValidateModuleConfig(ReportFieldType type, System.Text.Json.JsonElement? config)
    {
        return type switch
        {
            ReportFieldType.Photos => ValidatePhotosConfig(config),
            ReportFieldType.Measurements => ValidateMeasurementsConfig(config),
            ReportFieldType.Text or ReportFieldType.Number or ReportFieldType.Boolean or ReportFieldType.Date
                => config == null || !config.HasValue,
            _ => false
        };
    }

    private static bool ValidatePhotosConfig(System.Text.Json.JsonElement? config)
    {
        if (config == null || !config.HasValue)
            return false;

        try
        {
            var jsonElement = config.Value;
            if (jsonElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                return false;

            if (!jsonElement.TryGetProperty("requiredViews", out var requiredViews))
                return false;

            if (requiredViews.ValueKind != System.Text.Json.JsonValueKind.Array)
                return false;

            return requiredViews.GetArrayLength() > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool ValidateMeasurementsConfig(System.Text.Json.JsonElement? config)
    {
        if (config == null || !config.HasValue)
            return false;

        try
        {
            var jsonElement = config.Value;
            if (jsonElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                return false;

            if (!jsonElement.TryGetProperty("measurementTypes", out var measurementTypes))
                return false;

            if (measurementTypes.ValueKind != System.Text.Json.JsonValueKind.Array)
                return false;

            return measurementTypes.GetArrayLength() > 0;
        }
        catch
        {
            return false;
        }
    }
}
