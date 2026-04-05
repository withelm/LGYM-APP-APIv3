using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LgymApi.Application.Pagination;

namespace LgymApi.Infrastructure.Pagination;

public sealed record GridifyFieldDefinition
{
    public required string FieldName { get; init; }

    public required Type FieldType { get; init; }

    public bool AllowFilter { get; init; } = true;

    public bool AllowSort { get; init; } = true;
}

public sealed partial class FilterToGridifyAdapter : IFilterToGridifyAdapter
{
    private const int DefaultMaxPage = 21_474_837;
    private const int DefaultMaxPageSize = 100;
    private const int DefaultMaxNestingDepth = 3;

    private static readonly HashSet<FilterOperator> StringOperators =
    [
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.Contains,
        FilterOperator.StartsWith,
        FilterOperator.EndsWith,
        FilterOperator.In
    ];

    private static readonly HashSet<FilterOperator> ComparableOperators =
    [
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In,
        FilterOperator.GreaterThan,
        FilterOperator.GreaterThanOrEqual,
        FilterOperator.LessThan,
        FilterOperator.LessThanOrEqual
    ];

    private static readonly HashSet<FilterOperator> EqualityOperators =
    [
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In
    ];

    private readonly Dictionary<string, GridifyFieldDefinition> _fields;
    private readonly int _maxPage;
    private readonly int _maxPageSize;
    private readonly int _maxNestingDepth;

    public FilterToGridifyAdapter(
        IEnumerable<GridifyFieldDefinition> fields,
        int maxPage = DefaultMaxPage,
        int maxPageSize = DefaultMaxPageSize,
        int maxNestingDepth = DefaultMaxNestingDepth)
    {
        ArgumentNullException.ThrowIfNull(fields);

        if (maxPage < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPage), "Maximum page must be positive.");
        }

        if (maxPageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPageSize), "Maximum page size must be positive.");
        }

        if (maxNestingDepth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxNestingDepth), "Maximum nesting depth must be positive.");
        }

        _fields = BuildFieldMap(fields);
        _maxPage = maxPage;
        _maxPageSize = maxPageSize;
        _maxNestingDepth = maxNestingDepth;
    }

    public string Adapt(FilterInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        ValidatePage(input.Page);
        ValidatePageSize(input.PageSize);
        ValidateSortDescriptors(input.SortDescriptors);

        if (input.FilterGroups.Count == 0)
        {
            return string.Empty;
        }

        var groups = input.FilterGroups
            .Select(group => BuildGroup(group, 1))
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .ToArray();

        return groups.Length switch
        {
            0 => string.Empty,
            1 => groups[0],
            _ => $"({string.Join("&", groups)})"
        };
    }

    private static Dictionary<string, GridifyFieldDefinition> BuildFieldMap(IEnumerable<GridifyFieldDefinition> fields)
    {
        var map = new Dictionary<string, GridifyFieldDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in fields)
        {
            ArgumentNullException.ThrowIfNull(field);

            if (string.IsNullOrWhiteSpace(field.FieldName) || !FieldNamePattern().IsMatch(field.FieldName))
            {
                throw new ArgumentException($"Field '{field.FieldName}' is not a valid Gridify field name.", nameof(fields));
            }

            if (!map.TryAdd(field.FieldName, field))
            {
                throw new ArgumentException($"Duplicate field definition '{field.FieldName}'.", nameof(fields));
            }
        }

        if (map.Count == 0)
        {
            throw new ArgumentException("At least one field definition is required.", nameof(fields));
        }

        return map;
    }

    private void ValidatePage(int page)
    {
        if (page < 1 || page > _maxPage)
        {
            throw new ArgumentOutOfRangeException(nameof(page), page, $"Page must be between 1 and {_maxPage}.");
        }
    }

    private void ValidatePageSize(int pageSize)
    {
        if (pageSize < 1 || pageSize > _maxPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, $"Page size must be between 1 and {_maxPageSize}.");
        }
    }

    private void ValidateSortDescriptors(IEnumerable<SortDescriptor> sortDescriptors)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sortDescriptor in sortDescriptors)
        {
            if (string.IsNullOrWhiteSpace(sortDescriptor.FieldName) || !FieldNamePattern().IsMatch(sortDescriptor.FieldName))
            {
                throw new ArgumentException($"Sort field '{sortDescriptor.FieldName}' is not valid.", nameof(sortDescriptors));
            }

            if (!_fields.TryGetValue(sortDescriptor.FieldName, out var field) || !field.AllowSort)
            {
                throw new ArgumentException($"Sort field '{sortDescriptor.FieldName}' is not allowed.", nameof(sortDescriptors));
            }

            if (!seen.Add(sortDescriptor.FieldName))
            {
                throw new ArgumentException($"Duplicate sort field '{sortDescriptor.FieldName}' is not allowed.", nameof(sortDescriptors));
            }
        }
    }

    private string BuildGroup(FilterGroup group, int depth)
    {
        if (depth > _maxNestingDepth)
        {
            throw new ArgumentException($"Filter nesting depth cannot exceed {_maxNestingDepth}.", nameof(group));
        }

        if (!Enum.IsDefined(group.Operator))
        {
            throw new ArgumentException($"Group operator '{group.Operator}' is invalid.", nameof(group));
        }

        var fragments = new List<string>(group.Conditions.Count + group.Groups.Count);

        fragments.AddRange(group.Conditions.Select(BuildCondition));
        fragments.AddRange(group.Groups.Select(child => BuildGroup(child, depth + 1)));

        if (fragments.Count == 0)
        {
            throw new ArgumentException("Filter groups must contain at least one condition or child group.", nameof(group));
        }

        if (fragments.Count == 1)
        {
            return fragments[0];
        }

        var separator = group.Operator == GroupOperator.And ? "&" : "|";
        return $"({string.Join(separator, fragments)})";
    }

    private string BuildCondition(FilterCondition condition)
    {
        if (string.IsNullOrWhiteSpace(condition.FieldName) || !FieldNamePattern().IsMatch(condition.FieldName))
        {
            throw new ArgumentException($"Filter field '{condition.FieldName}' is not valid.", nameof(condition));
        }

        if (!_fields.TryGetValue(condition.FieldName, out var field) || !field.AllowFilter)
        {
            throw new ArgumentException($"Filter field '{condition.FieldName}' is not allowed.", nameof(condition));
        }

        if (!Enum.IsDefined(condition.Operator))
        {
            throw new ArgumentException($"Filter operator '{condition.Operator}' is not valid.", nameof(condition));
        }

        ValidateOperator(field, condition.Operator);

        if (condition.Operator == FilterOperator.In)
        {
            return BuildInCondition(condition, field);
        }

        var normalizedValue = NormalizeSingleValue(condition.Value, field.FieldType, condition.FieldName);
        return $"{condition.FieldName}{GetOperatorToken(condition.Operator)}{normalizedValue}";
    }

    private string BuildInCondition(FilterCondition condition, GridifyFieldDefinition field)
    {
        if (condition.Value is string || condition.Value is not IEnumerable values)
        {
            throw new ArgumentException($"IN operator for field '{condition.FieldName}' requires a non-empty collection.", nameof(condition));
        }

        var fragments = new List<string>();

        foreach (var value in values)
        {
            fragments.Add($"{condition.FieldName}={NormalizeSingleValue(value, field.FieldType, condition.FieldName)}");
        }

        if (fragments.Count == 0)
        {
            throw new ArgumentException($"IN operator for field '{condition.FieldName}' requires at least one value.", nameof(condition));
        }

        return fragments.Count == 1 ? fragments[0] : $"({string.Join("|", fragments)})";
    }

    private static void ValidateOperator(GridifyFieldDefinition field, FilterOperator filterOperator)
    {
        var normalizedType = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;
        HashSet<FilterOperator> allowedOperators = normalizedType switch
        {
            _ when normalizedType == typeof(string) => StringOperators,
            _ when IsComparableType(normalizedType) => ComparableOperators,
            _ => EqualityOperators
        };

        if (!allowedOperators.Contains(filterOperator))
        {
            throw new ArgumentException(
                $"Operator '{filterOperator}' is not allowed for field '{field.FieldName}' of type '{normalizedType.Name}'.",
                nameof(filterOperator));
        }
    }

    private static bool IsComparableType(Type type)
        => type.IsEnum
           || IsNumericType(type)
           || type == typeof(DateTime)
           || type == typeof(DateTimeOffset)
           || type == typeof(DateOnly)
           || type == typeof(TimeOnly);

    private static bool IsNumericType(Type type)
        => Type.GetTypeCode(type) is TypeCode.Byte
            or TypeCode.SByte
            or TypeCode.Int16
            or TypeCode.UInt16
            or TypeCode.Int32
            or TypeCode.UInt32
            or TypeCode.Int64
            or TypeCode.UInt64
            or TypeCode.Single
            or TypeCode.Double
            or TypeCode.Decimal;

    private static string NormalizeSingleValue(object? value, Type targetType, string fieldName)
    {
        if (value is null)
        {
            throw new ArgumentException($"Field '{fieldName}' requires a value.", nameof(value));
        }

        var normalizedType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        try
        {
            if (normalizedType == typeof(string))
            {
                if (value is not string stringValue)
                {
                    throw new InvalidCastException();
                }

                return EscapeValue(stringValue);
            }

            if (normalizedType == typeof(Guid))
            {
                return value switch
                {
                    Guid guid => guid.ToString("D"),
                    string guidText when Guid.TryParse(guidText, out var guid) => guid.ToString("D"),
                    _ => throw new InvalidCastException()
                };
            }

            if (normalizedType == typeof(bool))
            {
                return value switch
                {
                    bool boolean => boolean.ToString().ToLowerInvariant(),
                    string booleanText when bool.TryParse(booleanText, out var parsed) => parsed.ToString().ToLowerInvariant(),
                    _ => throw new InvalidCastException()
                };
            }

            if (normalizedType == typeof(DateTime))
            {
                return value switch
                {
                    DateTime dateTime => dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                    string text when DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) => parsed.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                    _ => throw new InvalidCastException()
                };
            }

            if (normalizedType == typeof(DateTimeOffset))
            {
                return value switch
                {
                    DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                    string text when DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) => parsed.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                    _ => throw new InvalidCastException()
                };
            }

            if (normalizedType == typeof(DateOnly))
            {
                return value switch
                {
                    DateOnly dateOnly => dateOnly.ToString("O", CultureInfo.InvariantCulture),
                    string text when DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) => parsed.ToString("O", CultureInfo.InvariantCulture),
                    _ => throw new InvalidCastException()
                };
            }

            if (normalizedType == typeof(TimeOnly))
            {
                return value switch
                {
                    TimeOnly timeOnly => timeOnly.ToString("O", CultureInfo.InvariantCulture),
                    string text when TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) => parsed.ToString("O", CultureInfo.InvariantCulture),
                    _ => throw new InvalidCastException()
                };
            }

            if (normalizedType.IsEnum)
            {
                return value switch
                {
                    string text when Enum.TryParse(normalizedType, text, true, out var parsed) => parsed!.ToString()!,
                    _ when value.GetType() == normalizedType => value.ToString()!,
                    _ => throw new InvalidCastException()
                };
            }

            if (IsNumericType(normalizedType))
            {
                var converted = Convert.ChangeType(value, normalizedType, CultureInfo.InvariantCulture);
                return Convert.ToString(converted, CultureInfo.InvariantCulture) ?? throw new InvalidCastException();
            }

            return EscapeValue(Convert.ToString(value, CultureInfo.InvariantCulture) ?? throw new InvalidCastException());
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
        {
            throw new ArgumentException(
                $"Value '{value}' is not valid for field '{fieldName}' of type '{normalizedType.Name}'.",
                nameof(value),
                exception);
        }
    }

    private static string GetOperatorToken(FilterOperator filterOperator)
        => filterOperator switch
        {
            FilterOperator.Equals => "=",
            FilterOperator.NotEquals => "!=",
            FilterOperator.Contains => "=*",
            FilterOperator.StartsWith => "^",
            FilterOperator.EndsWith => "$",
            FilterOperator.GreaterThan => ">",
            FilterOperator.GreaterThanOrEqual => ">=",
            FilterOperator.LessThan => "<",
            FilterOperator.LessThanOrEqual => "<=",
            FilterOperator.In => "=",
            _ => throw new ArgumentOutOfRangeException(nameof(filterOperator), filterOperator, "Unsupported filter operator.")
        };

    private static string EscapeValue(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);

        for (var index = 0; index < value.Length; index++)
        {
            if (StartsWithCaseInsensitive(value, index, "/i"))
            {
                builder.Append("\\/i");
                index++;
                continue;
            }

            var character = value[index];

            if (character is '\\' or '(' or ')' or '|' or '&' or ',')
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static bool StartsWithCaseInsensitive(string source, int startIndex, string value)
        => startIndex + value.Length <= source.Length
           && string.Compare(source, startIndex, value, 0, value.Length, StringComparison.OrdinalIgnoreCase) == 0;

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_.]*$", RegexOptions.CultureInvariant)]
    private static partial Regex FieldNamePattern();
}
