using System.Text.Json;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Features.Trainer.Validation;
using LgymApi.Domain.Enums;
using LgymApi.Resources;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ReportingValidatorsTests
{
    [Test]
    public void UpsertReportTemplateRequestValidator_Fails_WhenOrderNegative()
    {
        var validator = new UpsertReportTemplateRequestValidator();
        var request = new UpsertReportTemplateRequest
        {
            Name = "Weekly",
            Fields =
            [
                new ReportTemplateFieldRequest
                {
                    Key = "weight",
                    Label = "Weight",
                    Order = -1
                }
            ]
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.PropertyName.Contains("Order", StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public void SubmitReportRequestRequestValidator_AllowsEmptyAnswersDictionary()
    {
        var validator = new SubmitReportRequestRequestValidator();
        var request = new SubmitReportRequestRequest
        {
            Answers = new Dictionary<string, System.Text.Json.JsonElement>()
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void UpsertReportTemplateRequestValidator_Fails_ForInvalidPhotoRequiredView()
    {
        var validator = new UpsertReportTemplateRequestValidator();
        var request = new UpsertReportTemplateRequest
        {
            Name = "Weekly",
            Fields =
            [
                new ReportTemplateFieldRequest
                {
                    Key = "photos",
                    Label = "Photos",
                    Type = ReportFieldType.Photos,
                    Order = 0,
                    ModuleConfig = JsonDocument.Parse("""
                        {
                            "requiredViews": ["frontt"]
                        }
                        """).RootElement
                }
            ]
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That((result.Errors).Any(x => x.PropertyName == "Fields[0].ModuleConfig"), Is.True);
    }

    [TestCase("Foo")]
    [TestCase("Unknown")]
    public void UpsertReportTemplateRequestValidator_Fails_ForInvalidMeasurementType(string measurementType)
    {
        var validator = new UpsertReportTemplateRequestValidator();
        var request = new UpsertReportTemplateRequest
        {
            Name = "Weekly",
            Fields =
            [
                new ReportTemplateFieldRequest
                {
                    Key = "measurements",
                    Label = "Measurements",
                    Type = ReportFieldType.Measurements,
                    Order = 0,
                    ModuleConfig = JsonDocument.Parse($$"""
                        {
                            "measurementTypes": ["{{measurementType}}"]
                        }
                        """).RootElement
                }
            ]
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That((result.Errors).Any(x => x.PropertyName == "Fields[0].ModuleConfig"), Is.True);
    }

    [Test]
    public void UpsertRecurringReportAssignmentRequestValidator_Fails_WhenTemplateIdMissing()
    {
        var validator = new UpsertRecurringReportAssignmentRequestValidator();
        var request = new UpsertRecurringReportAssignmentRequest
        {
            TemplateId = string.Empty,
            IntervalValue = 1,
            StartsAt = DateTimeOffset.UtcNow
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That((result.Errors).Any(x => x.PropertyName == nameof(request.TemplateId)), Is.True);
    }

    [Test]
    public void UpsertRecurringReportAssignmentRequestValidator_Fails_WhenTemplateIdInvalid()
    {
        var validator = new UpsertRecurringReportAssignmentRequestValidator();
        var request = new UpsertRecurringReportAssignmentRequest
        {
            TemplateId = "not-an-id",
            IntervalValue = 1,
            StartsAt = DateTimeOffset.UtcNow
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That((result.Errors).Any(x => x.PropertyName == nameof(request.TemplateId)), Is.True);
    }

    [Test]
    public void UpsertRecurringReportAssignmentRequestValidator_Fails_WhenIntervalValueIsNotPositive()
    {
        var validator = new UpsertRecurringReportAssignmentRequestValidator();
        var request = new UpsertRecurringReportAssignmentRequest
        {
            TemplateId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.ReportTemplate>.New().ToString(),
            IntervalValue = 0,
            StartsAt = DateTimeOffset.UtcNow
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That((result.Errors).Any(x => x.PropertyName == nameof(request.IntervalValue)), Is.True);
    }

    [Test]
    public void UpsertRecurringReportAssignmentRequestValidator_Fails_WhenEndsAtIsBeforeStartsAt()
    {
        var validator = new UpsertRecurringReportAssignmentRequestValidator();
        var request = new UpsertRecurringReportAssignmentRequest
        {
            TemplateId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.ReportTemplate>.New().ToString(),
            IntervalValue = 1,
            StartsAt = DateTimeOffset.UtcNow,
            EndsAt = DateTimeOffset.UtcNow.AddDays(-1)
        };

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That((result.Errors).Any(x => x.ErrorMessage == Messages.InvalidDateRange), Is.True);
    }
}
