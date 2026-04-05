using FluentAssertions;
using LgymApi.Application.Pagination;
using LgymApi.Infrastructure.Pagination;
using NUnit.Framework;

namespace LgymApi.UnitTests.Pagination;

[TestFixture]
public sealed class PaginationWhitelistPolicyTests
{
    private sealed record TestProjection
    {
        public string Name { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public int StatusOrder { get; init; }
        public Guid Id { get; init; }
    }

    private static readonly FieldMapping[] DefaultMappings =
    [
        new() { FieldName = "name", MemberName = "Name" },
        new() { FieldName = "createdAt", MemberName = "CreatedAt" },
        new() { FieldName = "status", MemberName = "StatusOrder" },
        new() { FieldName = "id", MemberName = "Id", AllowSort = true, AllowFilter = false }
    ];

    private static readonly PaginationPolicy DefaultPolicy = new()
    {
        MaxPageSize = 100,
        DefaultPageSize = 20,
        DefaultSortField = "name",
        TieBreakerField = "id"
    };

    private static MapperRegistry CreateRegistryWithDefaults()
    {
        var registry = new MapperRegistry();
        registry.Register<TestProjection>(DefaultMappings);
        return registry;
    }

    [Test]
    public void MapperRegistry_RejectsUnknownField()
    {
        var registry = CreateRegistryWithDefaults();

        var found = registry.TryGetMapping<TestProjection>("nonExistentField", out var mapping);

        found.Should().BeFalse();
        mapping.Should().BeNull();
    }

    [Test]
    public void WhitelistPolicy_RejectsDuplicateSortField()
    {
        var registry = CreateRegistryWithDefaults();
        var policy = WhitelistPolicy.Create<TestProjection>(registry, DefaultPolicy);

        var sortDescriptors = new[]
        {
            new SortDescriptor { FieldName = "name" },
            new SortDescriptor { FieldName = "name", Descending = true }
        };

        var act = () => policy.ValidateSort(sortDescriptors);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Duplicate sort field*");
    }

    [Test]
    public void WhitelistPolicy_CapsPageSizeAtConfiguredMaximum()
    {
        var registry = CreateRegistryWithDefaults();
        var customPolicy = new PaginationPolicy
        {
            MaxPageSize = 50,
            DefaultPageSize = 20,
            DefaultSortField = "name",
            TieBreakerField = "id"
        };
        var policy = WhitelistPolicy.Create<TestProjection>(registry, customPolicy);

        var capped = policy.CapPageSize(200);

        capped.Should().Be(50);
    }

    [Test]
    public void GridifyMapperRegistry_AppliesWhitelistedMultiColumnSorting()
    {
        var registry = CreateRegistryWithDefaults();
        var policy = WhitelistPolicy.Create<TestProjection>(registry, DefaultPolicy);

        var sortDescriptors = new[]
        {
            new SortDescriptor { FieldName = "name" },
            new SortDescriptor { FieldName = "createdAt", Descending = true }
        };

        var act = () => policy.ValidateSort(sortDescriptors);

        act.Should().NotThrow();
    }

    [Test]
    public void GridifyMapperRegistry_AppendsDeterministicTieBreaker()
    {
        var registry = CreateRegistryWithDefaults();

        var hasTieBreaker = registry.TryGetMapping<TestProjection>(
            DefaultPolicy.TieBreakerField, out var tieBreaker);

        hasTieBreaker.Should().BeTrue();
        tieBreaker.Should().NotBeNull();
        tieBreaker!.FieldName.Should().Be("id");
        tieBreaker.AllowSort.Should().BeTrue();
    }

    [Test]
    public void GridifyMapperRegistry_RejectsDuplicateSortField()
    {
        var registry = CreateRegistryWithDefaults();
        var policy = WhitelistPolicy.Create<TestProjection>(registry, DefaultPolicy);

        var sortDescriptors = new[]
        {
            new SortDescriptor { FieldName = "createdAt" },
            new SortDescriptor { FieldName = "createdAt", Descending = true }
        };

        var act = () => policy.ValidateSort(sortDescriptors);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Duplicate sort field*");
    }
}
