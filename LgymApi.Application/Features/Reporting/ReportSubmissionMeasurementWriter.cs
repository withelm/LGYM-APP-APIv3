using System.Text.Json;
using LgymApi.Application.Features.Measurements;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using MeasurementEntity = LgymApi.Domain.Entities.Measurement;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Reporting;

public sealed class ReportSubmissionMeasurementWriter : IReportSubmissionMeasurementWriter
{
    private readonly IMeasurementRepository _measurementRepository;

    public ReportSubmissionMeasurementWriter(IMeasurementRepository measurementRepository)
    {
        _measurementRepository = measurementRepository;
    }

    public async Task StageMeasurementsAsync(
        UserEntity currentTrainee,
        ReportTemplate template,
        IReadOnlyDictionary<string, JsonElement> answers,
        DateTimeOffset submittedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (answers.Count == 0 || template.Fields.Count == 0)
        {
            return;
        }

        var measurementCandidates = CollectMeasurementCandidates(template, answers);
        if (measurementCandidates.Count == 0)
        {
            return;
        }

        var dayStartUtc = new DateTimeOffset(submittedAtUtc.UtcDateTime.Date, TimeSpan.Zero);
        var dayEndUtc = dayStartUtc.AddDays(1);
        var existingBodyParts = await _measurementRepository.GetExistingBodyPartsByUserAndCreatedAtRangeAsync(
            currentTrainee.Id,
            measurementCandidates.Keys.ToArray(),
            dayStartUtc,
            dayEndUtc,
            cancellationToken);

        foreach (var candidate in measurementCandidates.Values)
        {
            if (existingBodyParts.Contains(candidate.BodyPart))
            {
                continue;
            }

            await _measurementRepository.AddAsync(new MeasurementEntity
            {
                Id = Id<MeasurementEntity>.New(),
                UserId = currentTrainee.Id,
                BodyPart = candidate.BodyPart,
                Unit = candidate.Unit.ToString(),
                Value = candidate.Value,
                CreatedAt = submittedAtUtc
            }, cancellationToken);
        }
    }

    private static Dictionary<BodyParts, MeasurementCandidate> CollectMeasurementCandidates(
        ReportTemplate template,
        IReadOnlyDictionary<string, JsonElement> answers)
    {
        var candidates = new Dictionary<BodyParts, MeasurementCandidate>();

        foreach (var field in template.Fields.Where(field => field.Type == ReportFieldType.Measurements))
        {
            if (string.IsNullOrWhiteSpace(field.Key)
                || string.IsNullOrWhiteSpace(field.ModuleConfig)
                || !answers.TryGetValue(field.Key, out var fieldAnswer)
                || fieldAnswer.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            using var moduleConfigDocument = JsonDocument.Parse(field.ModuleConfig);
            if (!ReportingModuleConfigParser.TryNormalizeMeasurementModuleConfig(
                    moduleConfigDocument.RootElement,
                    out _,
                    out var allowedBodyParts))
            {
                continue;
            }

            var allowedBodyPartSet = allowedBodyParts.ToHashSet();
            foreach (var measurementProperty in fieldAnswer.EnumerateObject())
            {
                if (!ReportingModuleConfigParser.TryResolveBodyPart(measurementProperty.Name, out var bodyPart)
                    || !allowedBodyPartSet.Contains(bodyPart)
                    || candidates.ContainsKey(bodyPart)
                    || !TryParseMeasurementEntry(measurementProperty.Value, bodyPart, out var candidate))
                {
                    continue;
                }

                candidates.Add(bodyPart, candidate);
            }
        }

        return candidates;
    }

    private static bool TryParseMeasurementEntry(JsonElement rawValue, BodyParts bodyPart, out MeasurementCandidate candidate)
    {
        candidate = default;

        if (rawValue.ValueKind != JsonValueKind.Object
            || !rawValue.TryGetProperty("value", out var valueElement)
            || !valueElement.TryGetDouble(out var value)
            || !double.IsFinite(value)
            || value <= 0
            || !rawValue.TryGetProperty("unit", out var unitElement)
            || unitElement.ValueKind != JsonValueKind.String
            || !MeasurementUnitResolver.TryParseStoredUnit(unitElement.GetString(), out var unit)
            || !MeasurementUnitResolver.IsUnitAllowedForBodyPart(bodyPart, unit))
        {
            return false;
        }

        candidate = new MeasurementCandidate(bodyPart, unit, value);
        return true;
    }

    private readonly record struct MeasurementCandidate(BodyParts BodyPart, MeasurementUnits Unit, double Value);
}
