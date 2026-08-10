using System.Collections.Generic;
using DateCountdown.Core;
using Xunit;

namespace DateCountdown.Tests;

public sealed class CountdownPreferencesTests
{
    [Fact]
    public void Default_UsesDaysOnlyCountdowns()
    {
        CountdownPreferences preferences = CountdownPreferences.Default;

        Assert.False(preferences.SortCountdownsByDaysLeft);
        Assert.Equal(CountdownPreferences.DefaultDaysTextSize, preferences.DaysTextSize);
        Assert.Equal(CountdownPreferences.DefaultTitleTextSize, preferences.TitleTextSize);
        Assert.False(preferences.OpenWindowAtStartup);
        Assert.False(preferences.ShowDesktopCountdown);
        Assert.False(preferences.ReduceMotion);
        Assert.False(preferences.AutoDeleteExpiredCountdowns);
        Assert.False(preferences.ExperimentalFeaturesEnabled);
        Assert.False(preferences.ShowHoursWithinThreshold);
        Assert.Equal(CountdownHourDisplayMode.DaysOnly, preferences.HourDisplayMode);
        Assert.Equal(CountdownPreferences.DefaultHoursDisplayThresholdDays, preferences.HoursDisplayThresholdDays);
        Assert.Equal(CountdownPreferences.DefaultDesktopCountdownX, preferences.DesktopCountdownX);
        Assert.Equal(CountdownPreferences.DefaultDesktopCountdownY, preferences.DesktopCountdownY);
        Assert.Equal(CountdownPreferences.DefaultDesktopCountdownScale, preferences.DesktopCountdownScale);
        Assert.Equal(CountdownPreferences.DefaultDesktopCountdownCss, preferences.DesktopCountdownCss);
        Assert.Equal(
            [
                CountdownToolbarAction.Add,
                CountdownToolbarAction.Edit,
                CountdownToolbarAction.Delete,
                CountdownToolbarAction.StartupNotification
            ],
            preferences.ToolbarLayout.PrimaryActions);
        Assert.Equal(
            [
                CountdownToolbarAction.GlanceSurface,
                CountdownToolbarAction.DesktopOverlay
            ],
            preferences.ToolbarLayout.OverflowActions);
        Assert.Empty(preferences.DesktopOverlays);
    }

    [Theory]
    [InlineData(1, CountdownPreferences.MinDaysTextSize)]
    [InlineData(76.4, 76)]
    [InlineData(999, CountdownPreferences.MaxDaysTextSize)]
    public void CoerceDaysTextSize_ConstrainsValues(double value, double expected)
    {
        Assert.Equal(expected, CountdownPreferences.CoerceDaysTextSize(value));
    }

    [Theory]
    [InlineData(1, CountdownPreferences.MinTitleTextSize)]
    [InlineData(23.5, 24)]
    [InlineData(999, CountdownPreferences.MaxTitleTextSize)]
    public void CoerceTitleTextSize_ConstrainsValues(double value, double expected)
    {
        Assert.Equal(expected, CountdownPreferences.CoerceTitleTextSize(value));
    }

    [Fact]
    public void GetLegacyTextSizes_MapsPreviousDisplaySizeValues()
    {
        (double daysTextSize, double titleTextSize) = CountdownPreferences.GetLegacyTextSizes(CountdownDisplaySize.Large);

        Assert.Equal(84, daysTextSize);
        Assert.Equal(16, titleTextSize);
    }

    [Fact]
    public void With_ReplacesOnlySpecifiedValues()
    {
        CountdownPreferences preferences = new(
            sortCountdownsByDaysLeft: true,
            daysTextSize: 84,
            titleTextSize: 16,
            openWindowAtStartup: true,
            showDesktopCountdown: true,
            reduceMotion: true,
            autoDeleteExpiredCountdowns: true,
            experimentalFeaturesEnabled: true,
            showHoursWithinThreshold: true,
            hoursDisplayThresholdDays: 5,
            desktopCountdownX: 0.25,
            desktopCountdownY: 0.75,
            desktopCountdownScale: 1.5,
            desktopCountdownCss: ".countdown { text-align: left; }",
            toolbarLayout: new CountdownToolbarLayout(
                [CountdownToolbarAction.Add, CountdownToolbarAction.Delete],
                primaryActionCount: 1),
            desktopOverlays: new[] { new DesktopOverlaySettings("second", isEnabled: true) });

        CountdownPreferences updated = preferences.With(daysTextSize: 60);

        Assert.True(updated.SortCountdownsByDaysLeft);
        Assert.Equal(60, updated.DaysTextSize);
        Assert.Equal(16, updated.TitleTextSize);
        Assert.True(updated.OpenWindowAtStartup);
        Assert.True(updated.ShowDesktopCountdown);
        Assert.True(updated.ReduceMotion);
        Assert.True(updated.AutoDeleteExpiredCountdowns);
        Assert.True(updated.ExperimentalFeaturesEnabled);
        Assert.True(updated.ShowHoursWithinThreshold);
        Assert.Equal(CountdownHourDisplayMode.NearDeadline, updated.HourDisplayMode);
        Assert.Equal(5, updated.HoursDisplayThresholdDays);
        Assert.Equal(0.25, updated.DesktopCountdownX);
        Assert.Equal(0.75, updated.DesktopCountdownY);
        Assert.Equal(1.5, updated.DesktopCountdownScale);
        Assert.Equal(".countdown { text-align: left; }", updated.DesktopCountdownCss);
        Assert.Equal([CountdownToolbarAction.Add], updated.ToolbarLayout.PrimaryActions);
        Assert.Contains(CountdownToolbarAction.Delete, updated.ToolbarLayout.OverflowActions);
        Assert.Single(updated.DesktopOverlays);
        Assert.Equal("second", updated.DesktopOverlays[0].CountdownId);
    }

    [Fact]
    public void Constructor_NormalizesToolbarLayout()
    {
        CountdownToolbarLayout layout = new(
            [
                CountdownToolbarAction.Delete,
                CountdownToolbarAction.Delete,
                (CountdownToolbarAction)999,
                CountdownToolbarAction.Edit
            ],
            primaryActionCount: 99);

        Assert.Equal(
            [
                CountdownToolbarAction.Delete,
                CountdownToolbarAction.Edit,
                CountdownToolbarAction.Add,
                CountdownToolbarAction.StartupNotification,
                CountdownToolbarAction.GlanceSurface,
                CountdownToolbarAction.DesktopOverlay
            ],
            layout.Actions);
        Assert.Equal(layout.Actions.Count, layout.PrimaryActionCount);
    }

    [Fact]
    public void ToolbarLayout_PromoteToPrimary_MovesOverflowActionBeforeDivider()
    {
        CountdownToolbarLayout layout = new(
            [
                CountdownToolbarAction.Add,
                CountdownToolbarAction.Edit,
                CountdownToolbarAction.Delete,
                CountdownToolbarAction.StartupNotification,
                CountdownToolbarAction.GlanceSurface,
                CountdownToolbarAction.DesktopOverlay
            ],
            primaryActionCount: 4);

        CountdownToolbarLayout updated = layout.PromoteToPrimary(CountdownToolbarAction.DesktopOverlay);

        Assert.Equal(
            [
                CountdownToolbarAction.Add,
                CountdownToolbarAction.Edit,
                CountdownToolbarAction.Delete,
                CountdownToolbarAction.StartupNotification,
                CountdownToolbarAction.DesktopOverlay
            ],
            updated.PrimaryActions);
        Assert.Equal([CountdownToolbarAction.GlanceSurface], updated.OverflowActions);
    }

    [Fact]
    public void ToolbarLayoutPolicy_ResolveEffectiveLayout_PromotesDesktopOverlayWhenExperimentalFeaturesEnabled()
    {
        CountdownToolbarLayout layout = new(
            [
                CountdownToolbarAction.Add,
                CountdownToolbarAction.Edit,
                CountdownToolbarAction.Delete,
                CountdownToolbarAction.StartupNotification,
                CountdownToolbarAction.GlanceSurface,
                CountdownToolbarAction.DesktopOverlay
            ],
            primaryActionCount: 4);

        CountdownToolbarLayout updated = CountdownToolbarLayoutPolicy.ResolveEffectiveLayout(
            layout,
            experimentalFeaturesEnabled: true);

        Assert.True(updated.IsPrimary(CountdownToolbarAction.DesktopOverlay));
        Assert.DoesNotContain(CountdownToolbarAction.DesktopOverlay, updated.OverflowActions);
    }

    [Fact]
    public void ToolbarLayoutPolicy_ResolveEffectiveLayout_PreservesLayoutWhenExperimentalFeaturesDisabled()
    {
        CountdownToolbarLayout layout = CountdownToolbarLayout.Default;

        CountdownToolbarLayout updated = CountdownToolbarLayoutPolicy.ResolveEffectiveLayout(
            layout,
            experimentalFeaturesEnabled: false);

        Assert.Same(layout, updated);
    }

    [Fact]
    public void ParseActions_WithUnknownValues_UsesKnownActionsAndFillsMissingDefaults()
    {
        IReadOnlyList<CountdownToolbarAction> actions =
            CountdownToolbarLayout.ParseActions("Add,Unknown,StartupNotification,Add");

        Assert.Equal(
            [
                CountdownToolbarAction.Add,
                CountdownToolbarAction.StartupNotification,
                CountdownToolbarAction.Edit,
                CountdownToolbarAction.Delete,
                CountdownToolbarAction.GlanceSurface,
                CountdownToolbarAction.DesktopOverlay
            ],
            actions);
    }

    [Theory]
    [InlineData(-1, CountdownPreferences.MinHoursDisplayThresholdDays)]
    [InlineData(3, 3)]
    [InlineData(999, CountdownPreferences.MaxHoursDisplayThresholdDays)]
    public void Constructor_ConstrainsHoursDisplayThresholdDays(int thresholdDays, int expected)
    {
        CountdownPreferences preferences = new(hoursDisplayThresholdDays: thresholdDays);

        Assert.Equal(expected, preferences.HoursDisplayThresholdDays);
    }

    [Fact]
    public void With_CanSwitchToHoursOnlyMode()
    {
        CountdownPreferences preferences = new(showHoursWithinThreshold: true);

        CountdownPreferences updated = preferences.With(hourDisplayMode: CountdownHourDisplayMode.HoursOnly);

        Assert.Equal(CountdownHourDisplayMode.HoursOnly, updated.HourDisplayMode);
        Assert.False(updated.ShowHoursWithinThreshold);
    }

    [Fact]
    public void With_CanSwitchToDaysAndHoursMode()
    {
        CountdownPreferences preferences = new(showHoursWithinThreshold: true);

        CountdownPreferences updated = preferences.With(hourDisplayMode: CountdownHourDisplayMode.DaysAndHours);

        Assert.Equal(CountdownHourDisplayMode.DaysAndHours, updated.HourDisplayMode);
        Assert.False(updated.ShowHoursWithinThreshold);
    }

    [Theory]
    [InlineData(-1, 2, 0, 1)]
    [InlineData(0.33, 0.66, 0.33, 0.66)]
    public void Constructor_ConstrainsDesktopCountdownPosition(
        double x,
        double y,
        double expectedX,
        double expectedY)
    {
        CountdownPreferences preferences = new(desktopCountdownX: x, desktopCountdownY: y);

        Assert.Equal(expectedX, preferences.DesktopCountdownX);
        Assert.Equal(expectedY, preferences.DesktopCountdownY);
    }

    [Theory]
    [InlineData(0, CountdownPreferences.MinDesktopCountdownScale)]
    [InlineData(1.75, 1.75)]
    [InlineData(100, 100)]
    public void Constructor_ConstrainsDesktopCountdownScale(double scale, double expected)
    {
        CountdownPreferences preferences = new(desktopCountdownScale: scale);

        Assert.Equal(expected, preferences.DesktopCountdownScale);
    }

    [Fact]
    public void Constructor_UsesDefaultDesktopCountdownScaleForInvalidScale()
    {
        CountdownPreferences preferences = new(desktopCountdownScale: double.NaN);

        Assert.Equal(CountdownPreferences.DefaultDesktopCountdownScale, preferences.DesktopCountdownScale);
    }

    [Fact]
    public void Constructor_UsesDefaultCssForEmptyDesktopCountdownCss()
    {
        CountdownPreferences preferences = new(desktopCountdownCss: "");

        Assert.Equal(CountdownPreferences.DefaultDesktopCountdownCss, preferences.DesktopCountdownCss);
    }

    [Fact]
    public void Constructor_ResetsLegacyDesktopCountdownCssThatControlledPlacement()
    {
        string legacyCss = ".countdown-content { left: var(--position-x); top: var(--position-y); }";

        CountdownPreferences preferences = new(desktopCountdownCss: legacyCss);

        Assert.Equal(CountdownPreferences.DefaultDesktopCountdownCss, preferences.DesktopCountdownCss);
    }

    [Fact]
    public void DefaultDesktopCountdownCss_DoesNotOwnPositionOrSize()
    {
        Assert.DoesNotContain("position", CountdownPreferences.DefaultDesktopCountdownCss);
        Assert.DoesNotContain("font-size", CountdownPreferences.DefaultDesktopCountdownCss);
        Assert.DoesNotContain("--position", CountdownPreferences.DefaultDesktopCountdownCss);
        Assert.DoesNotContain("--days-size", CountdownPreferences.DefaultDesktopCountdownCss);
        Assert.DoesNotContain("--title-size", CountdownPreferences.DefaultDesktopCountdownCss);
    }

    [Fact]
    public void Constructor_TruncatesOversizedDesktopCountdownCss()
    {
        string css = new('x', CountdownPreferences.MaxDesktopCountdownCssLength + 10);

        CountdownPreferences preferences = new(desktopCountdownCss: css);

        Assert.Equal(CountdownPreferences.MaxDesktopCountdownCssLength, preferences.DesktopCountdownCss.Length);
    }

    [Fact]
    public void Constructor_DeduplicatesDesktopOverlaySettings()
    {
        CountdownPreferences preferences = new(
            desktopOverlays: new[]
            {
                new DesktopOverlaySettings("same", isEnabled: true),
                new DesktopOverlaySettings("same", isEnabled: true, positionX: 0.8)
            });

        Assert.Single(preferences.DesktopOverlays);
        Assert.Equal(CountdownPreferences.DefaultDesktopCountdownX, preferences.DesktopOverlays[0].PositionX);
    }

    [Fact]
    public void DesktopOverlaySettingsJson_RoundTripsPersistedSettings()
    {
        DesktopOverlaySettings overlay = new(
            "countdown-1",
            isEnabled: true,
            positionX: 0.25,
            positionY: 0.75,
            scale: 2.4,
            css: ".countdown { text-align: right; }");

        string json = DesktopOverlaySettingsJson.Serialize(new[] { overlay });
        IReadOnlyList<DesktopOverlaySettings> restored = DesktopOverlaySettingsJson.Deserialize(json);

        Assert.Single(restored);
        Assert.Equal("countdown-1", restored[0].CountdownId);
        Assert.True(restored[0].IsEnabled);
        Assert.Equal(0.25, restored[0].PositionX);
        Assert.Equal(0.75, restored[0].PositionY);
        Assert.Equal(2.4, restored[0].Scale);
        Assert.Equal(".countdown { text-align: right; }", restored[0].Css);
    }

    [Fact]
    public void DesktopOverlaySettingsJson_SkipsDisabledDefaultSettings()
    {
        string json = DesktopOverlaySettingsJson.Serialize(new[] { new DesktopOverlaySettings("countdown-1") });

        Assert.Equal("[]", json);
    }
}
