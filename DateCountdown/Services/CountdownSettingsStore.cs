using DateCountdown.Core;
using System;
using System.IO;
using Windows.Storage;

namespace DateCountdown.Services;

internal sealed class CountdownSettingsStore
{
    private readonly ApplicationDataContainer _localSettings;
    private readonly string _overlayFilePath;

    public CountdownSettingsStore()
    {
        _localSettings = ApplicationData.Current.LocalSettings;
        _overlayFilePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, DesktopOverlayFile.FileName);
    }

    public CountdownState Load(DateTimeOffset fallbackDate)
    {
        return CountdownStateSettings.Load(_localSettings.Values, fallbackDate);
    }

    public void Save(CountdownState state)
    {
        CountdownStateSettings.Save(_localSettings.Values, state);
    }

    public CountdownPreferences LoadPreferences()
    {
        return CountdownPreferencesSettings.Load(_localSettings.Values, _overlayFilePath);
    }

    public void SavePreferences(CountdownPreferences preferences)
    {
        CountdownPreferencesSettings.Save(_localSettings.Values, _overlayFilePath, preferences);
    }
}
