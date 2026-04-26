using DateCountdown.Core;
using System;
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
        string title = _localSettings.Values[CountdownSettingsKeys.Title] as string ?? string.Empty;
        DateTimeOffset targetDate = CountdownLogic.ReadDateValue(_localSettings.Values[CountdownSettingsKeys.TargetDate], fallbackDate);
        bool tileEnabled = ReadBool(CountdownSettingsKeys.TileEnabled);
        bool toastEnabled = ReadBool(CountdownSettingsKeys.ToastEnabled);

        return new CountdownState(title, targetDate, tileEnabled, toastEnabled);
    }

    public void Save(CountdownState state)
    {
        _localSettings.Values[CountdownSettingsKeys.Title] = state.Title;
        _localSettings.Values[CountdownSettingsKeys.TargetDate] = state.TargetDate;
        _localSettings.Values[CountdownSettingsKeys.TileEnabled] = state.TileEnabled;
        _localSettings.Values[CountdownSettingsKeys.ToastEnabled] = state.ToastEnabled;
    }

    private bool ReadBool(string key)
    {
        return _localSettings.Values[key] is bool value && value;
    }
}
