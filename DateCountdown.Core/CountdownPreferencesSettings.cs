using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DateCountdown.Core;

public static class CountdownPreferencesSettings
{
    private const int CurrentToolbarLayoutVersion = 2;

    public static CountdownPreferences Load(IDictionary<string, object> values, string overlayFilePath)
    {
        ArgumentNullException.ThrowIfNull(values);

        (double legacyDaysTextSize, double legacyTitleTextSize) = CountdownPreferences.GetLegacyTextSizes(
            CountdownPreferences.ParseDisplaySize(Read(values, CountdownSettingsKeys.DisplaySize)));

        CountdownHourDisplayMode hourDisplayMode = ReadHourDisplayMode(values) ??
            (ReadBool(values, CountdownSettingsKeys.ShowHoursWithinThreshold)
                ? CountdownHourDisplayMode.NearDeadline
                : CountdownHourDisplayMode.DaysOnly);

        return new CountdownPreferences(
            sortCountdownsByDaysLeft: ReadBool(values, CountdownSettingsKeys.SortCountdownsByDaysLeft),
            daysTextSize: ReadDouble(values, CountdownSettingsKeys.DaysTextSize) ?? legacyDaysTextSize,
            titleTextSize: ReadDouble(values, CountdownSettingsKeys.TitleTextSize) ?? legacyTitleTextSize,
            openWindowAtStartup: ReadBool(values, CountdownSettingsKeys.OpenWindowAtStartup),
            showDesktopCountdown: ReadBool(values, CountdownSettingsKeys.ShowDesktopCountdown),
            reduceMotion: ReadBool(values, CountdownSettingsKeys.ReduceMotion),
            autoDeleteExpiredCountdowns: ReadBool(values, CountdownSettingsKeys.AutoDeleteExpiredCountdowns),
            experimentalFeaturesEnabled: ReadBool(values, CountdownSettingsKeys.ExperimentalFeaturesEnabled),
            showHoursWithinThreshold: hourDisplayMode == CountdownHourDisplayMode.NearDeadline,
            hoursDisplayThresholdDays: ReadInt(values, CountdownSettingsKeys.HoursDisplayThresholdDays) ?? CountdownPreferences.DefaultHoursDisplayThresholdDays,
            hourDisplayMode: hourDisplayMode,
            desktopCountdownX: ReadDouble(values, CountdownSettingsKeys.DesktopCountdownX) ?? CountdownPreferences.DefaultDesktopCountdownX,
            desktopCountdownY: ReadDouble(values, CountdownSettingsKeys.DesktopCountdownY) ?? CountdownPreferences.DefaultDesktopCountdownY,
            desktopCountdownScale: ReadDouble(values, CountdownSettingsKeys.DesktopCountdownScale) ?? CountdownPreferences.DefaultDesktopCountdownScale,
            desktopCountdownCss: Read(values, CountdownSettingsKeys.DesktopCountdownCss) as string,
            toolbarLayout: ReadToolbarLayout(values),
            desktopOverlays: DesktopOverlayFile.Load(
                overlayFilePath,
                Read(values, CountdownSettingsKeys.DesktopOverlays) as string));
    }

    public static void Save(IDictionary<string, object> values, string overlayFilePath, CountdownPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(preferences);

        values[CountdownSettingsKeys.SortCountdownsByDaysLeft] = preferences.SortCountdownsByDaysLeft;
        values[CountdownSettingsKeys.DaysTextSize] = preferences.DaysTextSize;
        values[CountdownSettingsKeys.TitleTextSize] = preferences.TitleTextSize;
        values[CountdownSettingsKeys.OpenWindowAtStartup] = preferences.OpenWindowAtStartup;
        values[CountdownSettingsKeys.ReduceMotion] = preferences.ReduceMotion;
        values[CountdownSettingsKeys.AutoDeleteExpiredCountdowns] = preferences.AutoDeleteExpiredCountdowns;
        values[CountdownSettingsKeys.ExperimentalFeaturesEnabled] = preferences.ExperimentalFeaturesEnabled;
        values[CountdownSettingsKeys.HourDisplayMode] = preferences.HourDisplayMode.ToString();
        values[CountdownSettingsKeys.ShowHoursWithinThreshold] = preferences.ShowHoursWithinThreshold;
        values[CountdownSettingsKeys.HoursDisplayThresholdDays] = preferences.HoursDisplayThresholdDays;
        values[CountdownSettingsKeys.ToolbarActions] = CountdownToolbarLayout.SerializeActions(preferences.ToolbarLayout.Actions);
        values[CountdownSettingsKeys.ToolbarPrimaryActionCount] = preferences.ToolbarLayout.PrimaryActionCount;
        values[CountdownSettingsKeys.ToolbarLayoutVersion] = CurrentToolbarLayoutVersion;
        DesktopOverlayFile.Save(overlayFilePath, preferences.DesktopOverlays);
        values.Remove(CountdownSettingsKeys.DesktopOverlays);
        values.Remove(CountdownSettingsKeys.DisplaySize);
        values.Remove(CountdownSettingsKeys.ShowDesktopCountdown);
        values.Remove(CountdownSettingsKeys.DesktopCountdownX);
        values.Remove(CountdownSettingsKeys.DesktopCountdownY);
        values.Remove(CountdownSettingsKeys.DesktopCountdownScale);
        values.Remove(CountdownSettingsKeys.DesktopCountdownCss);
    }

    private static object? Read(IDictionary<string, object> values, string key)
    {
        return values.TryGetValue(key, out object? value) ? value : null;
    }

    private static bool ReadBool(IDictionary<string, object> values, string key)
    {
        return Read(values, key) is bool value && value;
    }

    private static double? ReadDouble(IDictionary<string, object> values, string key)
    {
        return Read(values, key) switch
        {
            double doubleValue => doubleValue,
            float floatValue => floatValue,
            int intValue => intValue,
            long longValue => longValue,
            string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedValue) => parsedValue,
            _ => null
        };
    }

    private static int? ReadInt(IDictionary<string, object> values, string key)
    {
        return ReadDouble(values, key) is double value && double.IsFinite(value)
            ? (int)Math.Round(value)
            : null;
    }

    private static CountdownHourDisplayMode? ReadHourDisplayMode(IDictionary<string, object> values)
    {
        return Read(values, CountdownSettingsKeys.HourDisplayMode) is string text &&
            Enum.TryParse(text, ignoreCase: true, out CountdownHourDisplayMode mode)
                ? mode
                : null;
    }

    private static CountdownToolbarLayout ReadToolbarLayout(IDictionary<string, object> values)
    {
        string? storedActions = Read(values, CountdownSettingsKeys.ToolbarActions) as string;
        int? storedPrimaryActionCount = ReadInt(values, CountdownSettingsKeys.ToolbarPrimaryActionCount);
        int? storedLayoutVersion = ReadInt(values, CountdownSettingsKeys.ToolbarLayoutVersion);
        if (ShouldMigrateToolbarLayout(storedLayoutVersion, storedActions, storedPrimaryActionCount))
        {
            return CountdownToolbarLayout.Default;
        }

        return new CountdownToolbarLayout(
            CountdownToolbarLayout.ParseActions(storedActions)
                .Select(action => (CountdownToolbarAction?)action),
            storedPrimaryActionCount);
    }

    private static bool ShouldMigrateToolbarLayout(
        int? storedLayoutVersion,
        string? storedActions,
        int? storedPrimaryActionCount)
    {
        if (!storedLayoutVersion.HasValue || storedLayoutVersion.Value < CurrentToolbarLayoutVersion)
        {
            return true;
        }

        return storedPrimaryActionCount == 2 && (
            string.Equals(storedActions, "Edit,Add,StartupNotification,GlanceSurface,Delete", StringComparison.Ordinal) ||
            string.Equals(storedActions, "Edit,Add,StartupNotification,GlanceSurface,Delete,DesktopOverlay", StringComparison.Ordinal));
    }
}
