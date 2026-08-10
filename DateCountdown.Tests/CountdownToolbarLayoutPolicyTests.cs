using DateCountdown.Core;
using System;
using Xunit;

namespace DateCountdown.Tests;

public sealed class CountdownToolbarLayoutPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(CountdownToolbarAction.StartupNotification)]
    [InlineData(CountdownToolbarAction.GlanceSurface)]
    [InlineData(CountdownToolbarAction.Edit)]
    [InlineData(CountdownToolbarAction.Delete)]
    [InlineData(CountdownToolbarAction.DesktopOverlay)]
    public void IsActionVisible_WithEmptyState_HidesCountdownActions(CountdownToolbarAction action)
    {
        Assert.False(CountdownToolbarLayoutPolicy.IsActionVisible(
            action,
            CountdownState.CreateEmpty(),
            experimentalFeaturesEnabled: true,
            isDesktopOverlayAvailable: true));
    }

    [Fact]
    public void IsActionVisible_AddIsAlwaysVisible()
    {
        Assert.True(CountdownToolbarLayoutPolicy.IsActionVisible(
            CountdownToolbarAction.Add,
            CountdownState.CreateEmpty(),
            experimentalFeaturesEnabled: false,
            isDesktopOverlayAvailable: false));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public void IsActionVisible_DesktopOverlay_RequiresExperimentalFeaturesAndAvailability(
        bool experimentalFeaturesEnabled,
        bool isDesktopOverlayAvailable,
        bool expected)
    {
        Assert.Equal(expected, CountdownToolbarLayoutPolicy.IsActionVisible(
            CountdownToolbarAction.DesktopOverlay,
            CreateState(),
            experimentalFeaturesEnabled,
            isDesktopOverlayAvailable));
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, false, false, false)]
    public void IsActionEnabled_GlanceSurface_RequiresWindows11OrTileOrCommittableDraft(
        bool isWindows11OrGreater,
        bool tileEnabled,
        bool canCommitDraft,
        bool expected)
    {
        Assert.Equal(expected, CountdownToolbarLayoutPolicy.IsActionEnabled(
            CountdownToolbarAction.GlanceSurface,
            CreateState(tileEnabled: tileEnabled),
            experimentalFeaturesEnabled: false,
            isDesktopOverlayAvailable: false,
            isWindows11OrGreater,
            canCommitDraft));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void IsActionEnabled_StartupNotification_RequiresToastOrCommittableDraft(
        bool toastEnabled,
        bool canCommitDraft,
        bool expected)
    {
        Assert.Equal(expected, CountdownToolbarLayoutPolicy.IsActionEnabled(
            CountdownToolbarAction.StartupNotification,
            CreateState(toastEnabled: toastEnabled),
            experimentalFeaturesEnabled: false,
            isDesktopOverlayAvailable: false,
            isWindows11OrGreater: true,
            canCommitDraft));
    }

    [Fact]
    public void IsActionEnabled_WithEmptyState_AllowsOnlyAdd()
    {
        foreach (CountdownToolbarAction action in Enum.GetValues<CountdownToolbarAction>())
        {
            bool enabled = CountdownToolbarLayoutPolicy.IsActionEnabled(
                action,
                CountdownState.CreateEmpty(),
                experimentalFeaturesEnabled: true,
                isDesktopOverlayAvailable: true,
                isWindows11OrGreater: true,
                canCommitDraft: false);
            Assert.Equal(action == CountdownToolbarAction.Add, enabled);
        }
    }

    private static CountdownState CreateState(bool toastEnabled = false, bool tileEnabled = false)
    {
        return new CountdownState(
            new[] { new CountdownItem("selected", "Launch", Now.AddDays(3), toastEnabled) },
            selectedCountdownId: "selected",
            tileEnabled: tileEnabled,
            toastEnabled: toastEnabled);
    }
}
