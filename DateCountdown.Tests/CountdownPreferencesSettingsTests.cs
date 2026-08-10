using DateCountdown.Core;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace DateCountdown.Tests;

public sealed class CountdownPreferencesSettingsTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    private string OverlayFilePath => Path.Combine(_directory, DesktopOverlayFile.FileName);

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsPreferences()
    {
        Dictionary<string, object> values = new(StringComparer.Ordinal);
        CountdownPreferences preferences = new(
            sortCountdownsByDaysLeft: true,
            daysTextSize: 84,
            titleTextSize: 16,
            openWindowAtStartup: true,
            autoDeleteExpiredCountdowns: true,
            hoursDisplayThresholdDays: 5,
            hourDisplayMode: CountdownHourDisplayMode.DaysAndHours,
            desktopOverlays: new[] { new DesktopOverlaySettings("selected", isEnabled: true) });

        CountdownPreferencesSettings.Save(values, OverlayFilePath, preferences);
        CountdownPreferences loaded = CountdownPreferencesSettings.Load(values, OverlayFilePath);

        Assert.True(loaded.SortCountdownsByDaysLeft);
        Assert.Equal(84, loaded.DaysTextSize);
        Assert.Equal(16, loaded.TitleTextSize);
        Assert.True(loaded.OpenWindowAtStartup);
        Assert.True(loaded.AutoDeleteExpiredCountdowns);
        Assert.Equal(5, loaded.HoursDisplayThresholdDays);
        Assert.Equal(CountdownHourDisplayMode.DaysAndHours, loaded.HourDisplayMode);
        Assert.Equal("selected", Assert.Single(loaded.DesktopOverlays).CountdownId);
    }

    [Fact]
    public void Load_ReadsLegacyDesktopCountdownValues()
    {
        Dictionary<string, object> values = new(StringComparer.Ordinal)
        {
            [CountdownSettingsKeys.ShowDesktopCountdown] = true,
            [CountdownSettingsKeys.DesktopCountdownX] = 0.2,
            [CountdownSettingsKeys.DesktopCountdownY] = 0.8,
            [CountdownSettingsKeys.DesktopCountdownScale] = 1.25,
            [CountdownSettingsKeys.DesktopCountdownCss] = ".countdown { color: red; }"
        };

        CountdownPreferences loaded = CountdownPreferencesSettings.Load(values, OverlayFilePath);

        Assert.True(loaded.ShowDesktopCountdown);
        Assert.Equal(0.2, loaded.DesktopCountdownX);
        Assert.Equal(0.8, loaded.DesktopCountdownY);
        Assert.Equal(1.25, loaded.DesktopCountdownScale);
        Assert.Equal(".countdown { color: red; }", loaded.DesktopCountdownCss);
    }

    [Fact]
    public void Save_RemovesConsumedLegacyValues()
    {
        Dictionary<string, object> values = new(StringComparer.Ordinal)
        {
            [CountdownSettingsKeys.DisplaySize] = "Compact",
            [CountdownSettingsKeys.ShowDesktopCountdown] = true,
            [CountdownSettingsKeys.DesktopCountdownX] = 0.2,
            [CountdownSettingsKeys.DesktopCountdownY] = 0.8,
            [CountdownSettingsKeys.DesktopCountdownScale] = 1.25,
            [CountdownSettingsKeys.DesktopCountdownCss] = ".countdown { color: red; }",
            [CountdownSettingsKeys.DesktopOverlays] = DesktopOverlaySettingsJson.Serialize(
                new[] { new DesktopOverlaySettings("legacy", isEnabled: true) })
        };

        CountdownPreferencesSettings.Save(values, OverlayFilePath, CountdownPreferences.Default);

        Assert.False(values.ContainsKey(CountdownSettingsKeys.DisplaySize));
        Assert.False(values.ContainsKey(CountdownSettingsKeys.ShowDesktopCountdown));
        Assert.False(values.ContainsKey(CountdownSettingsKeys.DesktopCountdownX));
        Assert.False(values.ContainsKey(CountdownSettingsKeys.DesktopCountdownY));
        Assert.False(values.ContainsKey(CountdownSettingsKeys.DesktopCountdownScale));
        Assert.False(values.ContainsKey(CountdownSettingsKeys.DesktopCountdownCss));
        Assert.False(values.ContainsKey(CountdownSettingsKeys.DesktopOverlays));
        Assert.True(File.Exists(OverlayFilePath));
    }

    [Fact]
    public void Load_LegacyDisplaySize_MapsTextSizes()
    {
        Dictionary<string, object> values = new(StringComparer.Ordinal)
        {
            [CountdownSettingsKeys.DisplaySize] = "Compact"
        };

        CountdownPreferences loaded = CountdownPreferencesSettings.Load(values, OverlayFilePath);

        Assert.Equal(60, loaded.DaysTextSize);
        Assert.Equal(13, loaded.TitleTextSize);
    }

    [Fact]
    public void Load_LegacyShowHoursWithinThreshold_MapsToNearDeadlineMode()
    {
        Dictionary<string, object> values = new(StringComparer.Ordinal)
        {
            [CountdownSettingsKeys.ShowHoursWithinThreshold] = true
        };

        CountdownPreferences loaded = CountdownPreferencesSettings.Load(values, OverlayFilePath);

        Assert.Equal(CountdownHourDisplayMode.NearDeadline, loaded.HourDisplayMode);
        Assert.True(loaded.ShowHoursWithinThreshold);
    }

    [Fact]
    public void Load_OutdatedToolbarLayoutVersion_ResetsToDefaultLayout()
    {
        Dictionary<string, object> values = new(StringComparer.Ordinal)
        {
            [CountdownSettingsKeys.ToolbarActions] = "Delete,Edit,Add",
            [CountdownSettingsKeys.ToolbarPrimaryActionCount] = 1,
            [CountdownSettingsKeys.ToolbarLayoutVersion] = 1
        };

        CountdownPreferences loaded = CountdownPreferencesSettings.Load(values, OverlayFilePath);

        Assert.Equal(CountdownToolbarLayout.Default.Actions, loaded.ToolbarLayout.Actions);
        Assert.Equal(CountdownToolbarLayout.Default.PrimaryActionCount, loaded.ToolbarLayout.PrimaryActionCount);
    }
}
