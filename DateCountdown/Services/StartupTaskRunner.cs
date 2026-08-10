using DateCountdown.Core;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace DateCountdown.Services;

internal sealed class StartupTaskRunner
{
    private readonly bool _isWindows11OrGreater;
    private readonly CountdownSettingsStore _settingsStore = new();
    private readonly ResourceLoader _resourceLoader = new();
    private readonly StartupNotificationService _startupNotificationService = new();
    private readonly StartMenuService _startMenuService = new();
    private readonly StartupFeatureService _startupFeatureService;

    public StartupTaskRunner(bool isWindows11OrGreater)
    {
        _isWindows11OrGreater = isWindows11OrGreater;
        _startupFeatureService = new StartupFeatureService(!isWindows11OrGreater, _startupNotificationService);
    }

    public async Task<bool> RunAsync()
    {
        DateTimeOffset now = DateTimeOffset.Now;
        CountdownPreferences preferences = _settingsStore.LoadPreferences();
        CountdownState state = ApplyPreferences(
            _startupFeatureService.NormalizeState(_settingsStore.Load(now)),
            preferences,
            now);
        CountdownDisplayText displayText = CreateDisplayText();

        foreach (CountdownItem countdown in state.Countdowns)
        {
            if (countdown.ToastEnabled)
            {
                string daysLeft = displayText.FormatCountdown(
                    countdown.TargetDate,
                    now,
                    preferences,
                    CultureInfo.CurrentCulture);
                _startupNotificationService.ShowStartupCountdownToast(
                    countdown.Id,
                    daysLeft,
                    displayText.FormatTitle(countdown.Title));
            }
        }

        if (!_isWindows11OrGreater && state.HasCountdowns && state.TileEnabled)
        {
            string daysLeft = displayText.FormatCountdown(
                state.TargetDate,
                now,
                preferences,
                CultureInfo.CurrentCulture);
            await _startMenuService.UpdateLiveTileAsync(daysLeft, displayText.FormatTitle(state.Title));
        }

        await _startupFeatureService.ReconcileAsync(state, preferences);
        return preferences.OpenWindowAtStartup;
    }

    private CountdownState ApplyPreferences(CountdownState state, CountdownPreferences preferences, DateTimeOffset now)
    {
        if (!preferences.AutoDeleteExpiredCountdowns)
        {
            return state;
        }

        CountdownState nextState = state.RemoveExpiredCountdowns(now);
        if (!ReferenceEquals(nextState, state))
        {
            _settingsStore.Save(nextState);
        }

        return nextState;
    }

    private CountdownDisplayText CreateDisplayText()
    {
        return CountdownDisplayText.FromResources(GetString);
    }

    private string GetString(string key)
    {
        try
        {
            return _resourceLoader.GetString(key);
        }
        catch
        {
            return string.Empty;
        }
    }
}
