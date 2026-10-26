using FluentAssertions;
using LgymApi.Application.WorkoutProgress.TrainingExecution;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class LocalCalendarDayTests
{
    [TestCase("UTC", "2026-10-22", "2026-10-22T00:00:00Z", "2026-10-23T00:00:00Z")]
    [TestCase("UTC", "0001-01-01", "0001-01-01T00:00:00Z", "0001-01-02T00:00:00Z")]
    [TestCase("Europe/Warsaw", "2026-10-22", "2026-10-21T22:00:00+00:00", "2026-10-22T22:00:00+00:00")]
    [TestCase("Asia/Kathmandu", "2026-10-22", "2026-10-21T18:15:00+00:00", "2026-10-22T18:15:00+00:00")]
    [TestCase("America/St_Johns", "2026-10-22", "2026-10-22T02:30:00+00:00", "2026-10-23T02:30:00+00:00")]
    [TestCase("Europe/Warsaw", "2026-03-29", "2026-03-28T23:00:00+00:00", "2026-03-29T22:00:00+00:00")]
    [TestCase("Europe/Warsaw", "2026-10-25", "2026-10-24T22:00:00+00:00", "2026-10-25T23:00:00+00:00")]
    public void ToUtcTimeRange_ConvertsEachLocalMidnightIndependently(string timeZone, string date, string expectedFrom, string expectedTo)
    {
        var range = new LocalCalendarDay(DateOnly.Parse(date), timeZone).ToUtcTimeRange();

        range.FromUtc.Should().Be(DateTimeOffset.Parse(expectedFrom));
        range.ToUtc.Should().Be(DateTimeOffset.Parse(expectedTo));
    }

    [Test]
    public void IsValid_RejectsUnknownZone()
    {
        LocalCalendarDay.IsValid(new DateOnly(2026, 10, 22), "Not/AZone").Should().BeFalse();
    }

    [TestCase("Pacific Standard Time")]
    [TestCase("Not/AZone")]
    [TestCase("UTC+02")]
    [TestCase("")]
    public void Constructor_RejectsNonIanaTimeZone(string timeZone)
    {
        var create = () => new LocalCalendarDay(new DateOnly(2026, 10, 22), timeZone);

        create.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Constructor_RejectsDayWhoseNextMidnightIsSkipped()
    {
        var create = () => new LocalCalendarDay(new DateOnly(2011, 12, 29), "Pacific/Apia");

        create.Should().Throw<ArgumentException>();
        LocalCalendarDay.IsValid(new DateOnly(2011, 12, 29), "Pacific/Apia").Should().BeFalse();
    }

    [TestCase("Etc/UTC", "9999-12-31")]
    [TestCase("Etc/GMT-14", "0001-01-01")]
    [TestCase("Pacific/Apia", "2011-12-30")]
    [TestCase("America/Havana", "2026-03-08")]
    [TestCase("America/Havana", "2026-11-01")]
    public void Constructor_RejectsDatesWithoutRepresentablePositiveUtcRange(string timeZone, string date)
    {
        var localDate = DateOnly.Parse(date);
        var create = () => new LocalCalendarDay(localDate, timeZone);

        create.Should().Throw<ArgumentException>();
        LocalCalendarDay.IsValid(localDate, timeZone).Should().BeFalse();
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void UtcTimeRange_RejectsEmptyOrReversedBounds(int hours)
    {
        var instant = DateTimeOffset.UtcNow;

        var create = () => new UtcTimeRange(instant, instant.AddHours(hours));

        create.Should().Throw<ArgumentException>();
    }
}
