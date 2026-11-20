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

        Assert.That(() => { act(); }, Throws.InstanceOf<ArgumentException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*Sort field 'unknown' is not allowed.*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
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

        Assert.That(() => { act(); }, Throws.InstanceOf<ArgumentException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*Duplicate sort field 'lastName' is not allowed.*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
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

        Assert.That(input.SortDescriptors, Has.Exactly(2).Items);
    }
}
