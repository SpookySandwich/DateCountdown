using DateCountdown.Core;
using System;
using Xunit;

namespace DateCountdown.Tests;

public sealed class CountdownTimeZoneTests
{
    [Fact]
    public void CreateTargetDate_UsesAnywhereOnEarthOffset()
    {
        DateTimeOffset target = CountdownTimeZone.CreateTargetDate(
            new DateTime(2026, 5, 8),
            new TimeSpan(23, 59, 0),
            CountdownTimeZone.AnywhereOnEarthId);

        Assert.Equal(TimeSpan.FromHours(-12), target.Offset);
        Assert.Equal(new DateTime(2026, 5, 9, 11, 59, 0), target.UtcDateTime);
    }

    [Fact]
    public void FormatTargetDateTime_IncludesSelectedZoneOffset()
    {
        DateTimeOffset target = new(2026, 5, 8, 23, 59, 0, TimeSpan.FromHours(-12));

        string text = CountdownTimeZone.FormatTargetDateTime(
            target,
            CountdownTimeZone.AnywhereOnEarthId,
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.EndsWith("(UTC-12:00)", text);
    }
}
