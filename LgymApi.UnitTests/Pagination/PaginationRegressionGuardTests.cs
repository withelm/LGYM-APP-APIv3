using FluentAssertions;
using LgymApi.Application.Pagination;
using LgymApi.Infrastructure.Pagination;
using NUnit.Framework;

namespace LgymApi.UnitTests.Pagination;

[TestFixture]
public sealed class PaginationRegressionGuardTests
{
    private sealed record RegressionProjection
    {
        public string Name { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public int StatusOrder { get; init; }
        public Guid Id { get; init; }
        public string Email { get; init; } = string.Empty;
    }

    private static readonly FieldMapping[] DefaultMappings =
    [
        new() { FieldName = "name", MemberName = "Name" },
        new() { FieldName = "createdAt", MemberName = "CreatedAt" },
        new() { FieldName = "statusOrder", MemberName = "StatusOrder" },
        new() { FieldName = "id", MemberName = "Id", AllowSort = true, AllowFilter = false },
        new() { FieldName = "email", MemberName = "Email" }
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
        registry.Register<RegressionProjection>(DefaultMappings);
        return registry;
    }

    [Test]
    public void GridifyMapperRegistry_RejectsUnknownField()
    {
        var registry = CreateRegistryWithDefaults();

        var found = registry.TryGetMapping<RegressionProjection>("nonExistentField", out var mapping);

        found.Should().BeFalse("unknown fields must be rejected by the mapper registry");
        mapping.Should().BeNull();
    }

    [Test]
    public void GridifyMapperRegistry_RejectsUnknownSortField_ViaWhitelistPolicy()
    {
        var registry = CreateRegistryWithDefaults();
        var policy = WhitelistPolicy.Create<RegressionProjection>(registry, DefaultPolicy);

        var act = () => policy.ValidateSort([
            new SortDescriptor { FieldName = "doesNotExist" }
        ]);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*'doesNotExist'*not a recognized field*");
    }

    [Test]
    public void GridifyAdapter_RejectsUnknownSortField()
    {
        var adapter = CreateAdapter();

        var input = new FilterInput
        {
            SortDescriptors = [new SortDescriptor { FieldName = "unknownField" }]
        };

        var act = () => adapter.Adapt(input);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Sort field 'unknownField' is not allowed*");
    }

    [Test]
    public void GridifyAdapter_RejectsUnknownFilterField()
    {
        var adapter = CreateAdapter();

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
                            FieldName = "nonExistentColumn",
                            Operator = FilterOperator.Equals,
                            Value = "test"
                        }
                    ]
                }
            ]
        };

        var act = () => adapter.Adapt(input);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Filter field 'nonExistentColumn' is not allowed*");
    }

    [Test]
    public void Pagination_PageBeyondTotal_ReportsNoNextPage()
    {
        var pagination = new Pagination<string>
        {
            Page = 5,
            PageSize = 10,
            TotalCount = 20,
            Items = []
        };

        pagination.TotalPages.Should().Be(2);
        pagination.HasNextPage.Should().BeFalse(
            "page 5 is beyond total pages (2), so there is no next page");
        pagination.HasPreviousPage.Should().BeTrue();
        pagination.Items.Should().BeEmpty();
    }

    [Test]
    public void Pagination_PageEqualsLastPage_ReportsNoNextPage()
    {
        var pagination = new Pagination<string>
        {
            Page = 3,
            PageSize = 10,
            TotalCount = 30,
            Items = []
        };

        pagination.TotalPages.Should().Be(3);
        pagination.HasNextPage.Should().BeFalse();
        pagination.HasPreviousPage.Should().BeTrue();
    }

    [Test]
    public void GridifyAdapter_RejectsDuplicateSortFields()
    {
        var adapter = CreateAdapter();

        var input = new FilterInput
        {
            SortDescriptors =
            [
                new SortDescriptor { FieldName = "name", Descending = false },
                new SortDescriptor { FieldName = "name", Descending = true }
            ]
        };

        var act = () => adapter.Adapt(input);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Duplicate sort field 'name'*");
    }

    [Test]
    public void WhitelistPolicy_RejectsDuplicateSortFields()
    {
        var registry = CreateRegistryWithDefaults();
        var policy = WhitelistPolicy.Create<RegressionProjection>(registry, DefaultPolicy);

        var act = () => policy.ValidateSort([
            new SortDescriptor { FieldName = "name" },
            new SortDescriptor { FieldName = "name", Descending = true }
        ]);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Duplicate sort field*");
    }

    [Test]
    public void GridifyAdapter_NestedFilterGroups_NormalizeCorrectly()
    {
        var adapter = CreateAdapter();

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
                            FieldName = "name",
                            Operator = FilterOperator.Contains,
                            Value = "test"
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
                                    FieldName = "email",
                                    Operator = FilterOperator.Contains,
                                    Value = "gmail"
                                },
                                new FilterCondition
                                {
                                    FieldName = "email",
                                    Operator = FilterOperator.Contains,
                                    Value = "outlook"
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var result = adapter.Adapt(input);

        result.Should().NotBeNullOrWhiteSpace("nested filter groups should produce a valid Gridify filter string");
        result.Should().Contain("name", "outer condition field should be present");
        result.Should().Contain("email", "inner group condition field should be present");
    }

    [Test]
    public void GridifyAdapter_ExceedsMaxNestingDepth_Throws()
    {
        var adapter = CreateAdapter(maxNestingDepth: 2);

        var deeplyNested = new FilterGroup
        {
            Operator = GroupOperator.And,
            Conditions = [new FilterCondition { FieldName = "name", Operator = FilterOperator.Equals, Value = "deep" }]
        };

        var level2 = new FilterGroup
        {
            Operator = GroupOperator.And,
            Groups = [deeplyNested]
        };

        var level1 = new FilterGroup
        {
            Operator = GroupOperator.And,
            Groups = [level2]
        };

        var input = new FilterInput
        {
            FilterGroups = [level1]
        };

        var act = () => adapter.Adapt(input);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*nesting depth*");
    }

    [Test]
    public void MapperRegistry_RejectsDuplicateFieldMappingRegistration()
    {
        var registry = new MapperRegistry();

        var act = () => registry.Register<RegressionProjection>([
            new FieldMapping { FieldName = "name", MemberName = "Name" },
            new FieldMapping { FieldName = "name", MemberName = "Email" }
        ]);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Duplicate field mapping*");
    }

    [Test]
    public void MapperRegistry_RejectsEmptyFieldName()
    {
        var registry = new MapperRegistry();

        var act = () => registry.Register<RegressionProjection>([
            new FieldMapping { FieldName = "", MemberName = "Name" }
        ]);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Field name must not be empty*");
    }

    private static FilterToGridifyAdapter CreateAdapter(int maxNestingDepth = 3)
    {
        var fields = DefaultMappings.Select(m => new GridifyFieldDefinition
        {
            FieldName = m.FieldName,
            FieldType = typeof(RegressionProjection).GetProperty(m.MemberName)!.PropertyType,
            AllowFilter = m.AllowFilter,
            AllowSort = m.AllowSort
        });

        return new FilterToGridifyAdapter(fields, maxNestingDepth: maxNestingDepth);
    }
}
