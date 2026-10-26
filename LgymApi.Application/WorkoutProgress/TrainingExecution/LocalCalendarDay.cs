using NodaTime;

namespace LgymApi.Application.WorkoutProgress.TrainingExecution;

public sealed class LocalCalendarDay
{
    public DateOnly Date { get; }
    public string TimeZone { get; }

    public LocalCalendarDay(DateOnly date, string timeZone)
    {
        if (!TryCreateUtcTimeRange(date, timeZone, out _))
        {
            throw new ArgumentException("The local calendar day cannot be represented as a UTC range.", nameof(date));
        }

        Date = date;
        TimeZone = timeZone;
    }

    public UtcTimeRange ToUtcTimeRange()
    {
        return TryCreateUtcTimeRange(Date, TimeZone, out var range)
            ? range
            : throw new InvalidOperationException("A validated local calendar day must have a UTC range.");
    }

    public static bool IsValid(DateOnly? date, string? timeZone)
        => date.HasValue && TryCreateUtcTimeRange(date.Value, timeZone, out _);

    private static bool TryCreateUtcTimeRange(DateOnly date, string? timeZone, out UtcTimeRange range)
    {
        range = null!;
        if (date == DateOnly.MaxValue || !TryResolveTimeZone(timeZone, out var resolvedTimeZone))
        {
            return false;
        }

        try
        {
            if (!TryResolveUtcMidnight(date, resolvedTimeZone, out var fromUtc)
                || !TryResolveUtcMidnight(date.AddDays(1), resolvedTimeZone, out var toUtc))
            {
                return false;
            }

            range = new UtcTimeRange(fromUtc, toUtc);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryResolveUtcMidnight(DateOnly date, DateTimeZone timeZone, out DateTimeOffset utcMidnight)
    {
        utcMidnight = default;
        var mapping = timeZone.MapLocal(new LocalDate(date.Year, date.Month, date.Day).AtMidnight());
        if (mapping.Count != 1)
        {
            return false;
        }

        var instant = mapping.Single().ToInstant();
        if (instant < Instant.FromDateTimeOffset(DateTimeOffset.MinValue)
            || instant > Instant.FromDateTimeOffset(DateTimeOffset.MaxValue))
        {
            return false;
        }

        utcMidnight = instant.ToDateTimeOffset();
        return true;
    }

    private static bool TryResolveTimeZone(string? timeZone, out DateTimeZone resolvedTimeZone)
    {
        resolvedTimeZone = null!;
        if (string.IsNullOrWhiteSpace(timeZone) || !DateTimeZoneProviders.Tzdb.Ids.Contains(timeZone, StringComparer.Ordinal)) return false;
        resolvedTimeZone = DateTimeZoneProviders.Tzdb[timeZone];
        return true;
    }
}

public sealed class UtcTimeRange
{
    public DateTimeOffset FromUtc { get; }
    public DateTimeOffset ToUtc { get; }

    public UtcTimeRange(DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        FromUtc = fromUtc.ToUniversalTime();
        ToUtc = toUtc.ToUniversalTime();
        if (FromUtc >= ToUtc) throw new ArgumentException("The UTC range must have a positive duration.", nameof(toUtc));
    }

    public static UtcTimeRange ForLegacyUtcDay(DateTime createdAt)
    {
        if (!IsValidLegacyUtcDay(createdAt)) throw new ArgumentException("The legacy UTC day cannot be represented as a positive UTC range.", nameof(createdAt));

        var date = DateOnly.FromDateTime(createdAt);
        var fromUtc = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        return new UtcTimeRange(fromUtc, fromUtc.AddDays(1));
    }

    public static bool IsValidLegacyUtcDay(DateTime? createdAt)
        => createdAt.HasValue
            && createdAt.Value != default
            && DateOnly.FromDateTime(createdAt.Value) != DateOnly.MaxValue;
}
