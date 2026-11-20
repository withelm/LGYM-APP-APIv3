using LgymApi.Application.Pagination;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class PaginationContractTests
{
    [Test]
    public void PaginationContract_CalculatesMetadataForNonEmptyResult()
    {
        var pagination = new Pagination<string>
        {
            Page = 2,
            PageSize = 10,
            TotalCount = 25,
            Items = ["a", "b"]
        };

        Assert.That(pagination.TotalPages, Is.EqualTo(3));
        Assert.That(pagination.HasNextPage, Is.True);
        Assert.That(pagination.HasPreviousPage, Is.True);
        { var orderedItems = (pagination.Items).ToArray(); var previousIndex = -1; foreach (var expectedItem in new[] { "a", "b" }) { var nextIndex = Array.FindIndex(orderedItems, previousIndex + 1, item => Equals(item, expectedItem)); Assert.That(nextIndex, Is.GreaterThan(previousIndex), $"Expected {expectedItem} after index {previousIndex}"); previousIndex = nextIndex; } }
    }
}
