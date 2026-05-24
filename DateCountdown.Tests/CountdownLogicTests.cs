using DateCountdown.Core;
using System;
using Xunit;

namespace DateCountdown.Tests;

public sealed class CountdownLogicTests
{
    [Fact]
    public void CalculateDaysLeft_ReturnsOverdueForPastSameCalendarDate()
    {
        DateTimeOffset now = new(2026, 4, 25, 23, 59, 0, TimeSpan.Zero);
        DateTimeOffset targetDate = new(2026, 4, 25, 0, 1, 0, TimeSpan.Zero);

        Assert.Equal(-1, CountdownLogic.CalculateDaysLeft(targetDate, now));
    }

    [Fact]
    public void CalculateDaysLeft_RoundsUpPartialFutureDays()
    {
        DateTimeOffset now = new(2026, 4, 25, 23, 59, 0, TimeSpan.Zero);
        DateTimeOffset targetDate = new(2026, 4, 26, 0, 1, 0, TimeSpan.Zero);

        Assert.Equal(1, CountdownLogic.CalculateDaysLeft(targetDate, now));
    }

    [Theory]
    [InlineData(2026, 4, 23, 12, -2)]
    [InlineData(2026, 4, 25, 12, 0)]
    [InlineData(2026, 5, 8, 12, 13)]
    [InlineData(2026, 5, 8, 13, 14)]
    public void CalculateDaysLeft_ReturnsSignedRoundedDuration(int year, int month, int day, int hour, int expectedDays)
    {
        DateTimeOffset now = new(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset targetDate = new(year, month, day, hour, 0, 0, TimeSpan.Zero);

        Assert.Equal(expectedDays, CountdownLogic.CalculateDaysLeft(targetDate, now));
    }

    [Theory]
    [InlineData(2026, 4, 25, 23, 59, 2026, 4, 25, 23, 30, 1)]
    [InlineData(2026, 4, 25, 23, 59, 2026, 4, 25, 12, 0, 12)]
    [InlineData(2026, 4, 26, 2, 30, 2026, 4, 25, 12, 30, 14)]
    public void CalculateHoursLeft_UsesExactTargetTime(
        int targetYear,
        int targetMonth,
        int targetDay,
        int targetHour,
        int targetMinute,
        int nowYear,
        int nowMonth,
        int nowDay,
        int hour,
        int minute,
        int expectedHours)
    {
        DateTimeOffset targetDate = new(targetYear, targetMonth, targetDay, targetHour, targetMinute, 0, TimeSpan.Zero);
        DateTimeOffset now = new(nowYear, nowMonth, nowDay, hour, minute, 0, TimeSpan.Zero);

        Assert.Equal(expectedHours, CountdownLogic.CalculateHoursLeft(targetDate, now));
    }

    [Fact]
    public void CalculateHoursLeft_ReturnsZeroAfterTargetDateEnds()
    {
        DateTimeOffset targetDate = new(2026, 4, 26, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset now = new(2026, 4, 27, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(0, CountdownLogic.CalculateHoursLeft(targetDate, now));
    }

    [Fact]
    public void ReadDateValue_ReturnsDateTimeOffsetValue()
    {
        DateTimeOffset fallback = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset value = new(2026, 5, 8, 9, 30, 0, TimeSpan.FromHours(-4));

        Assert.Equal(value, CountdownLogic.ReadDateValue(value, fallback));
    }

    [Fact]
    public void ReadDateValue_ConvertsDateTimeValue()
    {
        DateTime fallback = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Local);
        DateTime value = new(2026, 5, 8, 9, 30, 0, DateTimeKind.Local);

        Assert.Equal(new DateTimeOffset(value), CountdownLogic.ReadDateValue(value, new DateTimeOffset(fallback)));
    }

    [Fact]
    public void ReadDateValue_ParsesCurrentCultureString()
    {
        using CultureScope scope = new("en-US");
        DateTimeOffset fallback = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        DateTimeOffset result = CountdownLogic.ReadDateValue("5/8/2026", fallback);

        Assert.Equal(new DateTime(2026, 5, 8), result.Date);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a date")]
    public void ReadDateValue_ReturnsFallbackForMissingOrInvalidValues(object? value)
    {
        DateTimeOffset fallback = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(fallback, CountdownLogic.ReadDateValue(value, fallback));
    }
}
