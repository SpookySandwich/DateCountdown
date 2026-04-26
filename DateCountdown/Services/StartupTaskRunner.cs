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

    public async Task RunAsync()
    {
        DateTimeOffset now = DateTimeOffset.Now;
        CountdownState state = _startupFeatureService.NormalizeState(_settingsStore.Load(now));
        CountdownDisplayText displayText = CreateDisplayText();

        foreach (CountdownItem countdown in state.Countdowns)
        {
            if (countdown.ToastEnabled)
            {
                string daysLeft = displayText.FormatDaysLeft(
                    CountdownLogic.CalculateDaysLeft(countdown.TargetDate, now),
                    CultureInfo.CurrentCulture);
                _startupNotificationService.ShowToast(daysLeft, displayText.FormatTitle(countdown.Title));
            }
        }

        if (!_isWindows11OrGreater && state.TileEnabled)
        {
            string daysLeft = displayText.FormatDaysLeft(
                CountdownLogic.CalculateDaysLeft(state.TargetDate, now),
                CultureInfo.CurrentCulture);
            await _startMenuService.UpdateLiveTileAsync(daysLeft, displayText.FormatTitle(state.Title));
        }

        await _startupFeatureService.ReconcileAsync(state);
    }

    private CountdownDisplayText CreateDisplayText()
    {
        return new CountdownDisplayText(
            GetString("AppName"),
            GetString("DaysLeftOneText"),
            GetString("DaysLeftManyText"));
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
