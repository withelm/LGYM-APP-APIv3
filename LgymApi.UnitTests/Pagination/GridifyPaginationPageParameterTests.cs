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

        Assert.That(() => { act(); }, Throws.InstanceOf<ArgumentOutOfRangeException>()
            .With.Message.Contains("Page must be between 1").IgnoreCase
            .And.Property(nameof(ArgumentOutOfRangeException.ParamName)).EqualTo("page"));
    }

    [Test]
    public void GridifyPagination_RejectsInvalidPageSize()
    {
        var adapter = FilterToGridifyAdapterTestFactory.Create();
        var input = new FilterInput { PageSize = -1 };

        var act = () => adapter.Adapt(input);

        Assert.That(() => { act(); }, Throws.InstanceOf<ArgumentOutOfRangeException>()
            .With.Message.Contains("Page size must be between 1").IgnoreCase
            .And.Property(nameof(ArgumentOutOfRangeException.ParamName)).EqualTo("pageSize"));
    }

    [Test]
    public void GridifyPagination_UsesDefaultRequestShape()
    {
        var filter = new FilterInput();

        Assert.That(filter.Page, Is.EqualTo(1));
        Assert.That(filter.PageSize, Is.EqualTo(20));
    }
}
