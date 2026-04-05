using FluentAssertions;
using LgymApi.Application.Pagination;

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

        query.Should().Be("(status=active&age>18&(role=coach|role=trainer)&createdAt<=2026-04-05T10:15:00.0000000Z)");
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

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Operator 'Contains' is not allowed for field 'age'*");
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

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Filter field 'unknownField' is not allowed.*");
    }

    [Test]
    public void GridifyPagination_RejectsInvalidFilterFieldShape()
    {
        var act = () => new FilterToGridifyAdapterTestFactoryProxy().CreateWithInvalidFieldName();

        act.Should().Throw<ArgumentException>()
            .WithMessage("*is not a valid Gridify field name.*");
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

        act.Should().Throw<ArgumentException>()
            .WithMessage("*requires at least one value*");
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

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Value 'eighteen' is not valid for field 'age'*");
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
