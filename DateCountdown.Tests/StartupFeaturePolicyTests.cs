using DateCountdown.Core;
using System;
using Xunit;

namespace DateCountdown.Tests;

public sealed class StartupFeaturePolicyTests
{
    private static readonly DateTimeOffset TargetDate = new(2026, 5, 8, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false, false, false, false, false)]
    [InlineData(false, true, false, false, true)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, true, true, false, true)]
    [InlineData(true, false, false, false, false)]
    [InlineData(true, true, false, false, true)]
    [InlineData(true, false, true, false, true)]
    [InlineData(true, true, true, false, true)]
    [InlineData(false, false, false, true, true)]
    [InlineData(true, false, false, true, true)]
    public void RequiresStartupTask_UsesNotificationsSupportedStartTileAndStartupWindow(
        bool supportsStartTile,
        bool toastEnabled,
        bool tileEnabled,
        bool openWindowAtStartup,
        bool expected)
    {
        StartupFeaturePolicy policy = new(supportsStartTile);
        CountdownState state = new("Launch", TargetDate, tileEnabled, toastEnabled);
        CountdownPreferences preferences = new(openWindowAtStartup: openWindowAtStartup);

        Assert.Equal(expected, policy.RequiresStartupTask(state, preferences));
    }

    [Fact]
    public void RequiresStartupTask_UsesAnyCountdownNotification()
    {
        StartupFeaturePolicy policy = new(supportsStartTile: false);
        CountdownState state = new CountdownState(
            new[]
            {
                new CountdownItem("first", "First", TargetDate, toastEnabled: true),
                new CountdownItem("second", "Second", TargetDate.AddDays(1), toastEnabled: false)
            },
            selectedCountdownId: "second",
            tileEnabled: false,
            toastEnabled: false);

        Assert.False(state.ToastEnabled);
        Assert.True(policy.RequiresStartupTask(state, CountdownPreferences.Default));
    }

    [Fact]
    public void NormalizeState_OnWin10_PreservesTileSetting()
    {
        StartupFeaturePolicy policy = new(supportsStartTile: true);
        CountdownState state = new("Launch", TargetDate, tileEnabled: true, toastEnabled: false);

        CountdownState normalized = policy.NormalizeState(state);

        Assert.True(normalized.TileEnabled);
        Assert.False(normalized.ToastEnabled);
    }

    [Fact]
    public void NormalizeState_OnWin11_ClearsTileSettingAndPreservesToast()
    {
        StartupFeaturePolicy policy = new(supportsStartTile: false);
        CountdownState state = new("Launch", TargetDate, tileEnabled: true, toastEnabled: true);

        CountdownState normalized = policy.NormalizeState(state);

        Assert.False(normalized.TileEnabled);
        Assert.True(normalized.ToastEnabled);
    }

    [Fact]
    public void NormalizeState_OnWin11_PreservesSelectedCountdown()
    {
        StartupFeaturePolicy policy = new(supportsStartTile: false);
        CountdownState state = new CountdownState(
            new[]
            {
                new CountdownItem("first", "First", TargetDate),
                new CountdownItem("second", "Second", TargetDate.AddDays(1))
            },
            selectedCountdownId: "second",
            tileEnabled: true,
            toastEnabled: false);

        CountdownState normalized = policy.NormalizeState(state);

        Assert.Equal(2, normalized.Countdowns.Count);
        Assert.Equal("second", normalized.SelectedCountdownId);
        Assert.Equal("Second", normalized.Title);
        Assert.False(normalized.TileEnabled);
    }
}
