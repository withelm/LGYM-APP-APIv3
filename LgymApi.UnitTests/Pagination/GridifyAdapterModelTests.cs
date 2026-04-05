using FluentAssertions;
using LgymApi.Application.Pagination;
using NUnit.Framework;

namespace LgymApi.UnitTests.Pagination;

[TestFixture]
public sealed class GridifyAdapterModelTests
{
    [Test]
    public void GridifyAdapterModel_SupportsNestedAndOrGroups()
    {
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

        input.FilterGroups.Should().HaveCount(1);
        input.FilterGroups[0].Operator.Should().Be(GroupOperator.And);
        input.FilterGroups[0].Groups.Should().ContainSingle();
        input.FilterGroups[0].Groups[0].Operator.Should().Be(GroupOperator.Or);
        input.FilterGroups[0].Groups[0].Conditions.Should().HaveCount(2);
    }

    [Test]
    public void GridifyAdapterModel_SupportsMultipleSortColumns()
    {
        var input = new FilterInput
        {
            SortDescriptors =
            [
                new SortDescriptor { FieldName = "lastName", Descending = false },
                new SortDescriptor { FieldName = "createdAt", Descending = true }
            ]
        };

        input.SortDescriptors.Should().HaveCount(2);
        input.SortDescriptors[0].FieldName.Should().Be("lastName");
        input.SortDescriptors[0].Descending.Should().BeFalse();
        input.SortDescriptors[1].FieldName.Should().Be("createdAt");
        input.SortDescriptors[1].Descending.Should().BeTrue();
    }
}
