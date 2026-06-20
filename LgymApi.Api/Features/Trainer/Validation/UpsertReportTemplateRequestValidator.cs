using FluentValidation;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Domain.Enums;
using LgymApi.Resources;
using System.Text.Json;

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
                .WithMessage(Messages.ReportFieldValidationFailed);
        });
    }

    private static bool ValidateModuleConfig(ReportFieldType type, JsonElement? config)
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

    private static bool ValidatePhotosConfig(JsonElement? config)
    {
        if (config == null || !config.HasValue)
        {
            return false;
        }

        try
        {
            var jsonElement = config.Value;
            if (jsonElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!jsonElement.TryGetProperty("requiredViews", out var requiredViews))
            {
                return false;
            }

            if (requiredViews.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return TryReadRequiredViews(requiredViews, out _);
        }
        catch
        {
            return false;
        }
    }

    private static bool ValidateMeasurementsConfig(JsonElement? config)
    {
        if (config == null || !config.HasValue)
        {
            return false;
        }

        try
        {
            var jsonElement = config.Value;
            if (jsonElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!jsonElement.TryGetProperty("measurementTypes", out var measurementTypes))
            {
                return false;
            }

            if (measurementTypes.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return TryReadMeasurementTypes(measurementTypes, out _);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadRequiredViews(JsonElement requiredViews, out HashSet<PhotoViewType> parsedViews)
    {
        parsedViews = [];

        foreach (var viewElement in requiredViews.EnumerateArray())
        {
            if (viewElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var viewName = viewElement.GetString();
            if (string.IsNullOrWhiteSpace(viewName)
                || !System.Enum.TryParse<PhotoViewType>(viewName.Trim(), true, out var viewType)
                || !System.Enum.IsDefined(viewType))
            {
                return false;
            }

            parsedViews.Add(viewType);
        }

        return parsedViews.Count > 0;
    }

    private static bool TryReadMeasurementTypes(JsonElement measurementTypes, out HashSet<BodyParts> parsedBodyParts)
    {
        parsedBodyParts = [];

        foreach (var measurementTypeElement in measurementTypes.EnumerateArray())
        {
            if (measurementTypeElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var bodyPartName = measurementTypeElement.GetString();
            if (string.IsNullOrWhiteSpace(bodyPartName)
                || !TryResolveBodyPart(bodyPartName, out var bodyPart)
                || bodyPart == BodyParts.Unknown)
            {
                return false;
            }

            parsedBodyParts.Add(bodyPart);
        }

        return parsedBodyParts.Count > 0;
    }

    private static bool TryResolveBodyPart(string bodyPartName, out BodyParts bodyPart)
    {
        var normalized = bodyPartName.Trim();
        switch (normalized.ToLowerInvariant())
        {
            case "weight":
                bodyPart = BodyParts.BodyWeight;
                return true;
            case "bodyfat":
                bodyPart = BodyParts.BodyFat;
                return true;
            case "thighs":
                bodyPart = BodyParts.Thigh;
                return true;
        }

        return System.Enum.TryParse<BodyParts>(normalized, true, out bodyPart)
               && System.Enum.IsDefined(bodyPart);
    }
}
