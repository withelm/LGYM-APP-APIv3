using System.Text.Json;
using LgymApi.Application.Pagination;
using LgymApi.Domain.Enums;

namespace LgymApi.UnitTests.Pagination;

[TestFixture]
public sealed class FilterToGridifyAdapterJsonElementTests
{
    [Test]
    public void Adapt_UnwrapsJsonElementStringValue()
    {
        var query = AdaptSingle("status", FilterOperator.Equals, JsonSerializer.SerializeToElement("active"));

        Assert.That(query, Is.EqualTo("status=active"));
    }

    [Test]
    public void Adapt_UnwrapsJsonElementInt64Value()
    {
        var query = AdaptSingle("age", FilterOperator.Equals, JsonSerializer.SerializeToElement(42));

        Assert.That(query, Is.EqualTo("age=42"));
    }

    [Test]
    public void Adapt_UnwrapsJsonElementDecimalNumberValue()
    {
        var query = AdaptSingle("score", FilterOperator.Equals, JsonSerializer.SerializeToElement(3.14));

        Assert.That(query, Is.EqualTo("score=3.14"));
    }

    [Test]
    public void Adapt_UnwrapsJsonElementTrueValue()
    {
        var query = AdaptSingle("isActive", FilterOperator.Equals, JsonSerializer.SerializeToElement(true));

        Assert.That(query, Is.EqualTo("isActive=true"));
    }

    [Test]
    public void Adapt_UnwrapsJsonElementFalseValue()
    {
        var query = AdaptSingle("isActive", FilterOperator.Equals, JsonSerializer.SerializeToElement(false));

        Assert.That(query, Is.EqualTo("isActive=false"));
    }

    [Test]
    public void Adapt_RejectsJsonElementNullValue()
    {
        var input = CreateSingleConditionInput("status", FilterOperator.Equals, JsonSerializer.SerializeToElement<string?>(null));
        var adapter = FilterToGridifyAdapterTestFactory.Create();

        var act = () => adapter.Adapt(input);

        Assert.That(() => { act(); }, Throws.InstanceOf<ArgumentException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*Field 'status' requires a value.*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void Adapt_RejectsJsonElementArrayValueForSingleValueFilter()
    {
        var input = CreateSingleConditionInput("status", FilterOperator.Equals, JsonSerializer.SerializeToElement(new[] { "coach", "trainer" }));
        var adapter = FilterToGridifyAdapterTestFactory.Create();

        var act = () => adapter.Adapt(input);

        Assert.That(() => { act(); }, Throws.InstanceOf<ArgumentException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*Value 'System.Collections.Generic.List`1[System.Object]' is not valid for field 'status'*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void Adapt_RejectsNestedJsonElementArrayValueForSingleValueFilter()
    {
        var input = CreateSingleConditionInput(
            "status",
            FilterOperator.Equals,
            JsonSerializer.SerializeToElement(new[] { new[] { "coach", "trainer" } }));
        var adapter = FilterToGridifyAdapterTestFactory.Create();

        var act = () => adapter.Adapt(input);

        Assert.That(() => { act(); }, Throws.InstanceOf<ArgumentException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*Value 'System.Collections.Generic.List`1[System.Object]' is not valid for field 'status'*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void Adapt_UnwrapsJsonElementEnumStringValue()
    {
        var query = AdaptSingle(
            "invitationStatus",
            FilterOperator.Equals,
            JsonSerializer.SerializeToElement(nameof(TrainerInvitationStatus.Pending)));

        Assert.That(query, Is.EqualTo("invitationStatus=Pending"));
    }

    [Test]
    public void Adapt_BuildsInConditionFromJsonElementStringArray()
    {
        var query = AdaptSingle(
            "role",
            FilterOperator.In,
            JsonSerializer.SerializeToElement(new[] { "coach", "trainer" }));

        Assert.That(query, Is.EqualTo("(role=coach|role=trainer)"));
    }

    [Test]
    public void Adapt_BuildsInConditionFromJsonElementIntegerArray()
    {
        var query = AdaptSingle(
            "age",
            FilterOperator.In,
            JsonSerializer.SerializeToElement(new[] { 1, 2 }));

        Assert.That(query, Is.EqualTo("(age=1|age=2)"));
    }

    [Test]
    public void Adapt_BuildsInConditionFromJsonElementEnumStringArray()
    {
        var query = AdaptSingle(
            "invitationStatus",
            FilterOperator.In,
            JsonSerializer.SerializeToElement(new[]
            {
                nameof(TrainerInvitationStatus.Pending),
                nameof(TrainerInvitationStatus.Accepted)
            }));

        Assert.That(query, Is.EqualTo("(invitationStatus=Pending|invitationStatus=Accepted)"));
    }

    private static string AdaptSingle(string fieldName, FilterOperator filterOperator, object? value)
    {
        var adapter = FilterToGridifyAdapterTestFactory.Create();
        var input = CreateSingleConditionInput(fieldName, filterOperator, value);

        return adapter.Adapt(input);
    }

    private static FilterInput CreateSingleConditionInput(string fieldName, FilterOperator filterOperator, object? value)
        => new()
        {
            FilterGroups =
            [
                new FilterGroup
                {
                    Operator = GroupOperator.And,
                    Conditions =
                    [
                        new FilterCondition
                        {
                            FieldName = fieldName,
                            Operator = filterOperator,
                            Value = value
                        }
                    ]
                }
            ]
        };
}
