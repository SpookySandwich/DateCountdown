using DateCountdown.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Windows.Storage;

namespace DateCountdown.Services;

internal sealed class CountdownSettingsStore
{
    private const int CurrentToolbarLayoutVersion = 2;

    private readonly ApplicationDataContainer _localSettings;

    public CountdownSettingsStore()
        : this(ApplicationData.Current.LocalSettings)
    {
    }

    public CountdownSettingsStore(ApplicationDataContainer localSettings)
    {
        _localSettings = localSettings;
    }

    public CountdownState Load(DateTimeOffset fallbackDate)
    {
        bool tileEnabled = ReadBool(CountdownSettingsKeys.TileEnabled);
        bool toastEnabled = ReadBool(CountdownSettingsKeys.ToastEnabled);
        IReadOnlyList<CountdownItem> countdowns = CountdownStateJson.DeserializeCountdowns(_localSettings.Values[CountdownSettingsKeys.Countdowns] as string);
        string selectedCountdownId = _localSettings.Values[CountdownSettingsKeys.SelectedCountdownId] as string ?? string.Empty;

        if (countdowns.Count > 0)
        {
            return new CountdownState(countdowns, selectedCountdownId, tileEnabled, toastEnabled);
        }

        string title = _localSettings.Values[CountdownSettingsKeys.Title] as string ?? string.Empty;
        if (!HasLegacyCountdown(title))
        {
            return CountdownState.CreateEmpty();
        }

        string? targetTimeZoneId = _localSettings.Values[CountdownSettingsKeys.TargetTimeZoneId] as string;
        DateTimeOffset targetDate = CountdownLogic.ReadDateValue(_localSettings.Values[CountdownSettingsKeys.TargetDate], fallbackDate);
        if (string.IsNullOrWhiteSpace(targetTimeZoneId))
        {
            targetDate = CountdownTimeZone.MigrateLegacyDateOnlyTarget(targetDate, targetTimeZoneId);
        }

        return new CountdownState(title, targetDate, tileEnabled, toastEnabled, targetTimeZoneId);
    }

    public void Save(CountdownState state)
    {
        _localSettings.Values[CountdownSettingsKeys.Countdowns] = CountdownStateJson.SerializeCountdowns(state.Countdowns);
        _localSettings.Values[CountdownSettingsKeys.SelectedCountdownId] = state.SelectedCountdownId;
        if (!state.HasCountdowns)
        {
            _localSettings.Values[CountdownSettingsKeys.Title] = string.Empty;
            _localSettings.Values.Remove(CountdownSettingsKeys.TargetDate);
            _localSettings.Values[CountdownSettingsKeys.TargetTimeZoneId] = CountdownTimeZone.DefaultTimeZoneId;
            _localSettings.Values[CountdownSettingsKeys.TileEnabled] = false;
            _localSettings.Values[CountdownSettingsKeys.ToastEnabled] = false;
            return;
        }

        _localSettings.Values[CountdownSettingsKeys.Title] = state.Title;
        _localSettings.Values[CountdownSettingsKeys.TargetDate] = state.TargetDate;
        _localSettings.Values[CountdownSettingsKeys.TargetTimeZoneId] = state.TargetTimeZoneId;
        _localSettings.Values[CountdownSettingsKeys.TileEnabled] = state.TileEnabled;
        _localSettings.Values[CountdownSettingsKeys.ToastEnabled] = state.AnyToastEnabled;
    }

    public CountdownPreferences LoadPreferences()
    {
        (double legacyDaysTextSize, double legacyTitleTextSize) = CountdownPreferences.GetLegacyTextSizes(
            CountdownPreferences.ParseDisplaySize(_localSettings.Values[CountdownSettingsKeys.DisplaySize]));

        CountdownHourDisplayMode hourDisplayMode = ReadHourDisplayMode() ??
            (ReadBool(CountdownSettingsKeys.ShowHoursWithinThreshold)
                ? CountdownHourDisplayMode.NearDeadline
                : CountdownHourDisplayMode.DaysOnly);

        return new CountdownPreferences(
            sortCountdownsByDaysLeft: ReadBool(CountdownSettingsKeys.SortCountdownsByDaysLeft),
            daysTextSize: ReadDouble(CountdownSettingsKeys.DaysTextSize) ?? legacyDaysTextSize,
            titleTextSize: ReadDouble(CountdownSettingsKeys.TitleTextSize) ?? legacyTitleTextSize,
            openWindowAtStartup: ReadBool(CountdownSettingsKeys.OpenWindowAtStartup),
            showDesktopCountdown: ReadBool(CountdownSettingsKeys.ShowDesktopCountdown),
            reduceMotion: ReadBool(CountdownSettingsKeys.ReduceMotion),
            autoDeleteExpiredCountdowns: ReadBool(CountdownSettingsKeys.AutoDeleteExpiredCountdowns),
            experimentalFeaturesEnabled: ReadBool(CountdownSettingsKeys.ExperimentalFeaturesEnabled),
            showHoursWithinThreshold: hourDisplayMode == CountdownHourDisplayMode.NearDeadline,
            hoursDisplayThresholdDays: ReadInt(CountdownSettingsKeys.HoursDisplayThresholdDays) ?? CountdownPreferences.DefaultHoursDisplayThresholdDays,
            hourDisplayMode: hourDisplayMode,
            desktopCountdownX: ReadDouble(CountdownSettingsKeys.DesktopCountdownX) ?? CountdownPreferences.DefaultDesktopCountdownX,
            desktopCountdownY: ReadDouble(CountdownSettingsKeys.DesktopCountdownY) ?? CountdownPreferences.DefaultDesktopCountdownY,
            desktopCountdownScale: ReadDouble(CountdownSettingsKeys.DesktopCountdownScale) ?? CountdownPreferences.DefaultDesktopCountdownScale,
            desktopCountdownCss: _localSettings.Values[CountdownSettingsKeys.DesktopCountdownCss] as string,
            toolbarLayout: ReadToolbarLayout(),
            desktopOverlays: DesktopOverlaySettingsJson.Deserialize(_localSettings.Values[CountdownSettingsKeys.DesktopOverlays] as string));
    }

    public void SavePreferences(CountdownPreferences preferences)
    {
        _localSettings.Values[CountdownSettingsKeys.SortCountdownsByDaysLeft] = preferences.SortCountdownsByDaysLeft;
        _localSettings.Values[CountdownSettingsKeys.DaysTextSize] = preferences.DaysTextSize;
        _localSettings.Values[CountdownSettingsKeys.TitleTextSize] = preferences.TitleTextSize;
        _localSettings.Values[CountdownSettingsKeys.OpenWindowAtStartup] = preferences.OpenWindowAtStartup;
        _localSettings.Values[CountdownSettingsKeys.ShowDesktopCountdown] = preferences.ShowDesktopCountdown;
        _localSettings.Values[CountdownSettingsKeys.ReduceMotion] = preferences.ReduceMotion;
        _localSettings.Values[CountdownSettingsKeys.AutoDeleteExpiredCountdowns] = preferences.AutoDeleteExpiredCountdowns;
        _localSettings.Values[CountdownSettingsKeys.ExperimentalFeaturesEnabled] = preferences.ExperimentalFeaturesEnabled;
        _localSettings.Values[CountdownSettingsKeys.HourDisplayMode] = preferences.HourDisplayMode.ToString();
        _localSettings.Values[CountdownSettingsKeys.ShowHoursWithinThreshold] = preferences.ShowHoursWithinThreshold;
        _localSettings.Values[CountdownSettingsKeys.HoursDisplayThresholdDays] = preferences.HoursDisplayThresholdDays;
        _localSettings.Values[CountdownSettingsKeys.DesktopCountdownX] = preferences.DesktopCountdownX;
        _localSettings.Values[CountdownSettingsKeys.DesktopCountdownY] = preferences.DesktopCountdownY;
        _localSettings.Values[CountdownSettingsKeys.DesktopCountdownScale] = preferences.DesktopCountdownScale;
        _localSettings.Values[CountdownSettingsKeys.DesktopCountdownCss] = preferences.DesktopCountdownCss;
        _localSettings.Values[CountdownSettingsKeys.ToolbarActions] = CountdownToolbarLayout.SerializeActions(preferences.ToolbarLayout.Actions);
        _localSettings.Values[CountdownSettingsKeys.ToolbarPrimaryActionCount] = preferences.ToolbarLayout.PrimaryActionCount;
        _localSettings.Values[CountdownSettingsKeys.ToolbarLayoutVersion] = CurrentToolbarLayoutVersion;
        _localSettings.Values[CountdownSettingsKeys.DesktopOverlays] = DesktopOverlaySettingsJson.Serialize(preferences.DesktopOverlays);
    }

    private bool HasLegacyCountdown(string title)
    {
        return !string.IsNullOrWhiteSpace(title) ||
            _localSettings.Values.ContainsKey(CountdownSettingsKeys.TargetDate);
    }

    private bool ReadBool(string key, bool fallback = false)
    {
        return _localSettings.Values[key] is bool value ? value : fallback;
    }

    private double? ReadDouble(string key)
    {
        object? value = _localSettings.Values[key];
        return value switch
        {
            double doubleValue => doubleValue,
            float floatValue => floatValue,
            int intValue => intValue,
            long longValue => longValue,
            string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedValue) => parsedValue,
            _ => null
        };
    }

    private int? ReadInt(string key)
    {
        return ReadDouble(key) is double value && double.IsFinite(value)
            ? (int)Math.Round(value)
            : null;
    }

    private CountdownHourDisplayMode? ReadHourDisplayMode()
    {
        return _localSettings.Values[CountdownSettingsKeys.HourDisplayMode] is string text &&
            Enum.TryParse(text, ignoreCase: true, out CountdownHourDisplayMode mode)
                ? mode
                : null;
    }

    private CountdownToolbarLayout ReadToolbarLayout()
    {
        string? storedActions = _localSettings.Values[CountdownSettingsKeys.ToolbarActions] as string;
        int? storedPrimaryActionCount = ReadInt(CountdownSettingsKeys.ToolbarPrimaryActionCount);
        int? storedLayoutVersion = ReadInt(CountdownSettingsKeys.ToolbarLayoutVersion);
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
