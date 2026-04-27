using DateCountdown.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using Windows.Storage;

namespace DateCountdown.Services;

internal sealed class CountdownSettingsStore
{
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
        DateTimeOffset targetDate = CountdownLogic.ReadDateValue(_localSettings.Values[CountdownSettingsKeys.TargetDate], fallbackDate);
        return new CountdownState(title, targetDate, tileEnabled, toastEnabled);
    }

    public void Save(CountdownState state)
    {
        _localSettings.Values[CountdownSettingsKeys.Countdowns] = CountdownStateJson.SerializeCountdowns(state.Countdowns);
        _localSettings.Values[CountdownSettingsKeys.SelectedCountdownId] = state.SelectedCountdownId;
        _localSettings.Values[CountdownSettingsKeys.Title] = state.Title;
        _localSettings.Values[CountdownSettingsKeys.TargetDate] = state.TargetDate;
        _localSettings.Values[CountdownSettingsKeys.TileEnabled] = state.TileEnabled;
        _localSettings.Values[CountdownSettingsKeys.ToastEnabled] = state.AnyToastEnabled;
    }

    public CountdownPreferences LoadPreferences()
    {
        (double legacyDaysTextSize, double legacyTitleTextSize) = CountdownPreferences.GetLegacyTextSizes(
            CountdownPreferences.ParseDisplaySize(_localSettings.Values[CountdownSettingsKeys.DisplaySize]));

        return new CountdownPreferences(
            ReadBool(CountdownSettingsKeys.SortCountdownsByDaysLeft),
            ReadDouble(CountdownSettingsKeys.DaysTextSize) ?? legacyDaysTextSize,
            ReadDouble(CountdownSettingsKeys.TitleTextSize) ?? legacyTitleTextSize,
            ReadBool(CountdownSettingsKeys.OpenWindowAtStartup));
    }

    public void SavePreferences(CountdownPreferences preferences)
    {
        _localSettings.Values[CountdownSettingsKeys.SortCountdownsByDaysLeft] = preferences.SortCountdownsByDaysLeft;
        _localSettings.Values[CountdownSettingsKeys.DaysTextSize] = preferences.DaysTextSize;
        _localSettings.Values[CountdownSettingsKeys.TitleTextSize] = preferences.TitleTextSize;
        _localSettings.Values[CountdownSettingsKeys.OpenWindowAtStartup] = preferences.OpenWindowAtStartup;
    }

    private bool ReadBool(string key)
    {
        return _localSettings.Values[key] is bool value && value;
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
}
