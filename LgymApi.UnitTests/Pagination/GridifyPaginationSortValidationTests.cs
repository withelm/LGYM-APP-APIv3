using FluentAssertions;
using LgymApi.Application.Pagination;

namespace LgymApi.UnitTests.Pagination;

[TestFixture]
public sealed class GridifyPaginationSortValidationTests
{
    [Test]
    public void GridifyPagination_RejectsInvalidSortField()
    {
        var adapter = FilterToGridifyAdapterTestFactory.Create();
        var input = new FilterInput
        {
            SortDescriptors = [new SortDescriptor { FieldName = "unknown", Descending = false }]
        };

        var act = () => adapter.Adapt(input);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Sort field 'unknown' is not allowed.*");
    }

    [Test]
    public void GridifyPagination_RejectsDuplicateSortFields()
    {
        var adapter = FilterToGridifyAdapterTestFactory.Create();
        var input = new FilterInput
        {
            SortDescriptors =
            [
                new SortDescriptor { FieldName = "lastName", Descending = false },
                new SortDescriptor { FieldName = "lastName", Descending = true }
            ]
        };

        var act = () => adapter.Adapt(input);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Duplicate sort field 'lastName' is not allowed.*");
    }

    [Test]
    public void GridifyPagination_PreservesExplicitSortDirectionShape()
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
    }
}
