using DateCountdown.Core;
using System;
using Xunit;

namespace DateCountdown.Tests;

public sealed class CountdownStateTests
{
    [Fact]
    public void Constructor_NormalizesNullTitleToEmptyString()
    {
        CountdownState state = new(null!, DateTimeOffset.UnixEpoch, tileEnabled: false, toastEnabled: false);

        Assert.Equal(string.Empty, state.Title);
    }

    [Fact]
    public void CreateDefault_UsesProvidedTargetDateAndDisabledOptions()
    {
        DateTimeOffset targetDate = new(2026, 5, 8, 0, 0, 0, TimeSpan.Zero);

        CountdownState state = CountdownState.CreateDefault(targetDate);

        Assert.Equal(string.Empty, state.Title);
        Assert.Equal(targetDate, state.TargetDate);
        Assert.False(state.TileEnabled);
        Assert.False(state.ToastEnabled);
    }

    [Fact]
    public void With_PreservesUnspecifiedValues()
    {
        DateTimeOffset targetDate = new(2026, 5, 8, 0, 0, 0, TimeSpan.Zero);
        CountdownState original = new("Launch", targetDate, tileEnabled: true, toastEnabled: false);

        CountdownState updated = original.With(toastEnabled: true);

        Assert.Equal("Launch", updated.Title);
        Assert.Equal(targetDate, updated.TargetDate);
        Assert.True(updated.TileEnabled);
        Assert.True(updated.ToastEnabled);
    }

    [Fact]
    public void With_AllowsExplicitEmptyTitle()
    {
        CountdownState original = new("Launch", DateTimeOffset.UnixEpoch, tileEnabled: true, toastEnabled: true);

        CountdownState updated = original.With(title: string.Empty);

        Assert.Equal(string.Empty, updated.Title);
    }
}
