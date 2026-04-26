using DateCountdown.Core;
using System;
using Xunit;

namespace DateCountdown.Tests;

public sealed class StartupFeaturePolicyTests
{
    private static readonly DateTimeOffset TargetDate = new(2026, 5, 8, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void RequiresStartupTask_OnWin10_WhenToastOrTileIsEnabled(bool toastEnabled, bool tileEnabled, bool expected)
    {
        StartupFeaturePolicy policy = new(supportsLiveTileStartup: true);
        CountdownState state = new("Launch", TargetDate, tileEnabled, toastEnabled);

        Assert.Equal(expected, policy.RequiresStartupTask(state));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public void RequiresStartupTask_OnWin11_IgnoresTileAndUsesToast(bool toastEnabled, bool tileEnabled, bool expected)
    {
        StartupFeaturePolicy policy = new(supportsLiveTileStartup: false);
        CountdownState state = new("Launch", TargetDate, tileEnabled, toastEnabled);

        Assert.Equal(expected, policy.RequiresStartupTask(state));
    }

    [Fact]
    public void NormalizeState_OnWin10_PreservesTileSetting()
    {
        StartupFeaturePolicy policy = new(supportsLiveTileStartup: true);
        CountdownState state = new("Launch", TargetDate, tileEnabled: true, toastEnabled: false);

        CountdownState normalized = policy.NormalizeState(state);

        Assert.True(normalized.TileEnabled);
        Assert.False(normalized.ToastEnabled);
    }

    [Fact]
    public void NormalizeState_OnWin11_ClearsTileSettingAndPreservesToast()
    {
        StartupFeaturePolicy policy = new(supportsLiveTileStartup: false);
        CountdownState state = new("Launch", TargetDate, tileEnabled: true, toastEnabled: true);

        CountdownState normalized = policy.NormalizeState(state);

        Assert.False(normalized.TileEnabled);
        Assert.True(normalized.ToastEnabled);
    }
}
