using LgymApi.Infrastructure.Pagination;

namespace LgymApi.UnitTests.Pagination;

internal static class FilterToGridifyAdapterTestFactory
{
    public static FilterToGridifyAdapter Create(int maxPageSize = 100, int maxNestingDepth = 3)
        => new(
            [
                new GridifyFieldDefinition { FieldName = "status", FieldType = typeof(string) },
                new GridifyFieldDefinition { FieldName = "role", FieldType = typeof(string) },
                new GridifyFieldDefinition { FieldName = "lastName", FieldType = typeof(string) },
                new GridifyFieldDefinition { FieldName = "createdAt", FieldType = typeof(DateTime) },
                new GridifyFieldDefinition { FieldName = "age", FieldType = typeof(int) },
                new GridifyFieldDefinition { FieldName = "isActive", FieldType = typeof(bool) },
                new GridifyFieldDefinition { FieldName = "id", FieldType = typeof(Guid) }
            ],
            maxPageSize: maxPageSize,
            maxNestingDepth: maxNestingDepth);
}
