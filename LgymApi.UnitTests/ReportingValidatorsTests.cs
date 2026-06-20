using FluentAssertions;
using System.Text.Json;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Features.Trainer.Validation;
using LgymApi.Domain.Enums;

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

        result.IsValid.Should().BeFalse();
        result.Errors.Any(e => e.PropertyName.Contains("Order", StringComparison.Ordinal)).Should().BeTrue();
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

        result.IsValid.Should().BeTrue();
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

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "Fields[0].ModuleConfig");
    }
}
