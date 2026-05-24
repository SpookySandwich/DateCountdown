using System;
using DateCountdown.Core;
using Xunit;

namespace DateCountdown.Tests;

public sealed class CountdownRefreshScheduleTests
{
    [Fact]
    public void GetDelayUntilNextLocalDateRefresh_UsesNextLocalMidnight()
    {
        DateTimeOffset now = new(2026, 5, 1, 12, 30, 0, TimeSpan.FromHours(-4));

        TimeSpan delay = CountdownRefreshSchedule.GetDelayUntilNextLocalDateRefresh(now);

        Assert.Equal(TimeSpan.FromHours(11.5) + TimeSpan.FromSeconds(1), delay);
    }

    [Fact]
    public void GetDelayUntilNextLocalDateRefresh_AddsBufferNearMidnight()
    {
        DateTimeOffset now = new(2026, 5, 1, 23, 59, 59, 500, TimeSpan.FromHours(-4));

        TimeSpan delay = CountdownRefreshSchedule.GetDelayUntilNextLocalDateRefresh(now);

        Assert.Equal(TimeSpan.FromMilliseconds(1500), delay);
    }

    [Fact]
    public void GetDelayUntilNextLocalHourRefresh_UsesNextLocalHour()
    {
        DateTimeOffset now = new(2026, 5, 1, 12, 30, 0, TimeSpan.FromHours(-4));

        TimeSpan delay = CountdownRefreshSchedule.GetDelayUntilNextLocalHourRefresh(now);

        Assert.Equal(TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(1), delay);
    }

    [Fact]
    public void GetDelayUntilNextCountdownRefresh_UsesHourlyRefreshWhenHoursEnabled()
    {
        DateTimeOffset now = new(2026, 5, 1, 12, 30, 0, TimeSpan.FromHours(-4));
        CountdownPreferences preferences = new(showHoursWithinThreshold: true);

        TimeSpan delay = CountdownRefreshSchedule.GetDelayUntilNextCountdownRefresh(now, preferences);

        Assert.Equal(TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(1), delay);
    }

    [Fact]
    public void GetDelayUntilNextCountdownRefresh_UsesTargetRelativeDayBoundary()
    {
        DateTimeOffset now = new(2026, 5, 1, 7, 0, 0, TimeSpan.Zero);
        CountdownItem countdown = new("target", "Target", new DateTimeOffset(2026, 5, 2, 8, 0, 0, TimeSpan.Zero));

        TimeSpan delay = CountdownRefreshSchedule.GetDelayUntilNextCountdownRefresh(
            now,
            new CountdownPreferences(),
            new[] { countdown });

        Assert.Equal(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1), delay);
    }

    [Fact]
    public void GetDelayUntilNextCountdownRefresh_UsesTargetRelativeHourBoundary()
    {
        DateTimeOffset now = new(2026, 5, 1, 12, 10, 0, TimeSpan.Zero);
        CountdownItem countdown = new("target", "Target", new DateTimeOffset(2026, 5, 1, 15, 30, 0, TimeSpan.Zero));

        TimeSpan delay = CountdownRefreshSchedule.GetDelayUntilNextCountdownRefresh(
            now,
            new CountdownPreferences(hourDisplayMode: CountdownHourDisplayMode.HoursOnly),
            new[] { countdown });

        Assert.Equal(TimeSpan.FromMinutes(20) + TimeSpan.FromSeconds(1), delay);
    }

    [Fact]
    public void GetDelayUntilNextCountdownRefresh_UsesNearDeadlineTransitionBoundary()
    {
        DateTimeOffset now = new(2026, 5, 1, 12, 10, 0, TimeSpan.Zero);
        CountdownItem countdown = new("target", "Target", new DateTimeOffset(2026, 5, 4, 12, 40, 0, TimeSpan.Zero));

        TimeSpan delay = CountdownRefreshSchedule.GetDelayUntilNextCountdownRefresh(
            now,
            new CountdownPreferences(showHoursWithinThreshold: true, hoursDisplayThresholdDays: 3),
            new[] { countdown });

        Assert.Equal(TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(1), delay);
    }

    [Fact]
    public void GetDelayUntilNextCountdownRefresh_UpdatesOverdueCountsOnTargetRelativeDayBoundary()
    {
        DateTimeOffset now = new(2026, 5, 2, 7, 30, 0, TimeSpan.Zero);
        CountdownItem countdown = new("target", "Target", new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero));

        TimeSpan delay = CountdownRefreshSchedule.GetDelayUntilNextCountdownRefresh(
            now,
            new CountdownPreferences(),
            new[] { countdown });

        Assert.Equal(TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(1), delay);
    }
}
