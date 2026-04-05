using FluentAssertions;
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

        pagination.TotalPages.Should().Be(3);
        pagination.HasNextPage.Should().BeTrue();
        pagination.HasPreviousPage.Should().BeTrue();
        pagination.Items.Should().ContainInOrder("a", "b");
    }
}
