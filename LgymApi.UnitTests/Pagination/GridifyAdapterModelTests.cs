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

        Assert.That(input.FilterGroups, Has.Exactly(1).Items);
        Assert.That(input.FilterGroups[0].Operator, Is.EqualTo(GroupOperator.And));
        Assert.That(input.FilterGroups[0].Groups, Has.Exactly(1).Items);
        Assert.That(input.FilterGroups[0].Groups[0].Operator, Is.EqualTo(GroupOperator.Or));
        Assert.That(input.FilterGroups[0].Groups[0].Conditions, Has.Exactly(2).Items);
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

        Assert.That(input.SortDescriptors, Has.Exactly(2).Items);
        Assert.That(input.SortDescriptors[0].FieldName, Is.EqualTo("lastName"));
        Assert.That(input.SortDescriptors[0].Descending, Is.False);
        Assert.That(input.SortDescriptors[1].FieldName, Is.EqualTo("createdAt"));
        Assert.That(input.SortDescriptors[1].Descending, Is.True);
    }
}
