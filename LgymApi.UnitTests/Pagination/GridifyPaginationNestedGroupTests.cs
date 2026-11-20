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

        Assert.That(query, Is.EqualTo("(status=active&(age>18|(role=coach|role=trainer)))"));
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

        Assert.That(() => { act(); }, Throws.InstanceOf<ArgumentException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*nesting depth cannot exceed 2*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
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

        Assert.That(() => { act(); }, Throws.InstanceOf<ArgumentException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*must contain at least one condition or child group*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
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
