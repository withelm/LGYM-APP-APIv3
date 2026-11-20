using LgymApi.Application.Pagination;
using LgymApi.Domain.Enums;

namespace LgymApi.UnitTests.Pagination;

[TestFixture]
public sealed class GridifyPaginationFilterValidationTests
{
    [Test]
    public void GridifyFilterAdapter_AcceptsKnownFieldsAndOperators()
    {
        var adapter = FilterToGridifyAdapterTestFactory.Create();
        var input = new FilterInput
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
                            FieldName = "status",
                            Operator = FilterOperator.Equals,
                            Value = "active"
                        },
                        new FilterCondition
                        {
                            FieldName = "age",
                            Operator = FilterOperator.GreaterThan,
                            Value = 18
                        },
                        new FilterCondition
                        {
                            FieldName = "role",
                            Operator = FilterOperator.In,
                            Value = new[] { "coach", "trainer" }
                        },
                        new FilterCondition
                        {
                            FieldName = "createdAt",
                            Operator = FilterOperator.LessThanOrEqual,
                            Value = new DateTime(2026, 04, 05, 10, 15, 00, DateTimeKind.Utc)
                        }
                    ]
                }
            ]
        };

        var query = adapter.Adapt(input);

        Assert.That(query, Is.EqualTo("(status=active&age>18&(role=coach|role=trainer)&createdAt<=2026-04-05T10:15:00.0000000Z)"));
    }

    [Test]
    public void GridifyFilterAdapter_RejectsInvalidOperatorForFieldType()
    {
        var adapter = FilterToGridifyAdapterTestFactory.Create();
        var input = new FilterInput
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
                            FieldName = "age",
                            Operator = FilterOperator.Contains,
                            Value = 18
                        }
                    ]
                }
            ]
        };

        var act = () => adapter.Adapt(input);

        Assert.That(() => { act(); }, Throws.InstanceOf<ArgumentException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*Operator 'Contains' is not allowed for field 'age'*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void GridifyPagination_RejectsUnknownFilterField()
    {
        var adapter = FilterToGridifyAdapterTestFactory.Create();
        var input = new FilterInput
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
                            FieldName = "unknownField",
                            Operator = FilterOperator.Equals,
                            Value = "value"
                        }
                    ]
                }
            ]
        };

        var act = () => adapter.Adapt(input);

        Assert.That(() => { act(); }, Throws.InstanceOf<ArgumentException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*Filter field 'unknownField' is not allowed.*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void GridifyPagination_RejectsInvalidFilterFieldShape()
    {
        var act = () => new FilterToGridifyAdapterTestFactoryProxy().CreateWithInvalidFieldName();

        Assert.That(() => { act(); }, Throws.InstanceOf<ArgumentException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*is not a valid Gridify field name.*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void GridifyPagination_RejectsEmptyInList()
    {
        var adapter = FilterToGridifyAdapterTestFactory.Create();
        var input = new FilterInput
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
                            FieldName = "role",
                            Operator = FilterOperator.In,
                            Value = Array.Empty<string>()
                        }
                    ]
                }
            ]
        };

        var act = () => adapter.Adapt(input);

        Assert.That(() => { act(); }, Throws.InstanceOf<ArgumentException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*requires at least one value*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void GridifyPagination_RejectsWrongValueType()
    {
        var adapter = FilterToGridifyAdapterTestFactory.Create();
        var input = new FilterInput
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
                            FieldName = "age",
                            Operator = FilterOperator.GreaterThan,
                            Value = "eighteen"
                        }
                    ]
                }
            ]
        };

        var act = () => adapter.Adapt(input);

        Assert.That(() => { act(); }, Throws.InstanceOf<ArgumentException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*Value 'eighteen' is not valid for field 'age'*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void GridifyPagination_NormalizesEnumStringValueByName()
    {
        var adapter = FilterToGridifyAdapterTestFactory.Create();
        var input = new FilterInput
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
                            FieldName = "invitationStatus",
                            Operator = FilterOperator.Equals,
                            Value = nameof(TrainerInvitationStatus.Pending)
                        }
                    ]
                }
            ]
        };

        var query = adapter.Adapt(input);

        Assert.That(query, Is.EqualTo("invitationStatus=Pending"));
    }

    [Test]
    public void GridifyPagination_RejectsInvalidEnumValue()
    {
        var adapter = FilterToGridifyAdapterTestFactory.Create();
        var input = new FilterInput
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
                            FieldName = "invitationStatus",
                            Operator = FilterOperator.Equals,
                            Value = "NotAStatus"
                        }
                    ]
                }
            ]
        };

        var act = () => adapter.Adapt(input);

        Assert.That(() => { act(); }, Throws.InstanceOf<ArgumentException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*Value 'NotAStatus' is not valid for field 'invitationStatus'*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    private sealed class FilterToGridifyAdapterTestFactoryProxy
    {
        public object CreateWithInvalidFieldName()
            => new LgymApi.Infrastructure.Pagination.FilterToGridifyAdapter(
                [
                    new LgymApi.Infrastructure.Pagination.GridifyFieldDefinition
                    {
                        FieldName = "bad field name",
                        FieldType = typeof(string)
                    }
                ]);
    }
}
