using FluentAssertions;
using LgymApi.Application.Pagination;

namespace LgymApi.UnitTests.Pagination;

[TestFixture]
public sealed class GridifyPaginationNestedGroupTests
{
    [Test]
    public void GridifyPagination_SupportsNestedAndOrGroups()
    {
        var adapter = FilterToGridifyAdapterTestFactory.Create();

        var query = adapter.Adapt(BuildNestedFilterInput());

        query.Should().Be("(status=active&(age>18|(role=coach|role=trainer)))");
    }

    [Test]
    public void GridifyFilterAdapter_RejectsExcessiveNestingDepth()
    {
        var adapter = FilterToGridifyAdapterTestFactory.Create(maxNestingDepth: 2);

        var act = () => adapter.Adapt(new FilterInput
        {
            FilterGroups =
            [
                new FilterGroup
                {
                    Operator = GroupOperator.And,
                    Groups =
                    [
                        new FilterGroup
                        {
                            Operator = GroupOperator.Or,
                            Groups =
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
                                            Value = 18
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*nesting depth cannot exceed 2*");
    }

    [Test]
    public void GridifyPagination_RejectsInvalidNestedGroupTree()
    {
        var adapter = FilterToGridifyAdapterTestFactory.Create();
        var input = new FilterInput
        {
            FilterGroups =
            [
                new FilterGroup
                {
                    Operator = GroupOperator.And
                }
            ]
        };

        var act = () => adapter.Adapt(input);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*must contain at least one condition or child group*");
    }

    private static FilterInput BuildNestedFilterInput() => new()
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
                    }
                ],
                Groups =
                [
                    new FilterGroup
                    {
                        Operator = GroupOperator.Or,
                        Conditions =
                        [
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
                            }
                        ]
                    }
                ]
            }
        ]
    };
}
