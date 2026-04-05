using FluentAssertions;
using LgymApi.Application.Pagination;

namespace LgymApi.UnitTests.Pagination;

[TestFixture]
public sealed class GridifyPaginationPageParameterTests
{
    [Test]
    public void GridifyPagination_RejectsInvalidPageNumber()
    {
        var adapter = FilterToGridifyAdapterTestFactory.Create();
        var input = new FilterInput { Page = 0 };

        var act = () => adapter.Adapt(input);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Page must be between 1*\nParameter 'page'*");
    }

    [Test]
    public void GridifyPagination_RejectsInvalidPageSize()
    {
        var adapter = FilterToGridifyAdapterTestFactory.Create();
        var input = new FilterInput { PageSize = -1 };

        var act = () => adapter.Adapt(input);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Page size must be between 1*\nParameter 'pageSize'*");
    }

    [Test]
    public void GridifyPagination_UsesDefaultRequestShape()
    {
        var filter = new FilterInput();

        filter.Page.Should().Be(1);
        filter.PageSize.Should().Be(20);
    }
}
