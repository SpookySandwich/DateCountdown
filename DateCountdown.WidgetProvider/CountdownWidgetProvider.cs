using DateCountdown.Core;
using Microsoft.Windows.ApplicationModel.Resources;
using Microsoft.Windows.Widgets;
using Microsoft.Windows.Widgets.Providers;
using System;
using System.Collections.Generic;
using System.Threading;
using Windows.Storage;

namespace DateCountdown.WidgetProvider;

internal sealed class CountdownWidgetProvider : IWidgetProvider, IDisposable
{
    private readonly ManualResetEventSlim _exitEvent = new(false);
    private readonly HashSet<string> _runningWidgetIds = new(StringComparer.Ordinal);

    public void Activate(WidgetContext widgetContext)
    {
        AddWidget(widgetContext);
        UpdateWidget(widgetContext);
    }

    public void CreateWidget(WidgetContext widgetContext)
    {
        AddWidget(widgetContext);
        UpdateWidget(widgetContext);
    }

    public void Deactivate(string widgetId)
    {
        RemoveWidget(widgetId);
    }

    public void DeleteWidget(string widgetId, string customState)
    {
        RemoveWidget(widgetId);
    }

    public void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs)
    {
        if (string.Equals(actionInvokedArgs.Verb, "refresh", StringComparison.OrdinalIgnoreCase))
        {
            UpdateWidget(actionInvokedArgs.WidgetContext);
        }
    }

    public void OnWidgetContextChanged(WidgetContextChangedArgs contextChangedArgs)
    {
        AddWidget(contextChangedArgs.WidgetContext);
        UpdateWidget(contextChangedArgs.WidgetContext);
    }

    public void WaitForExit()
    {
        _exitEvent.Wait();
    }

    public void Dispose()
    {
        _exitEvent.Dispose();
    }

    private void AddWidget(WidgetContext widgetContext)
    {
        if (!string.Equals(widgetContext.DefinitionId, CountdownWidgetContent.DefinitionId, StringComparison.Ordinal))
        {
            return;
        }

        _runningWidgetIds.Add(widgetContext.Id);
        _exitEvent.Reset();
    }

    private void RemoveWidget(string widgetId)
    {
        _runningWidgetIds.Remove(widgetId);
        if (_runningWidgetIds.Count == 0)
        {
            _exitEvent.Set();
        }
    }

    private static void UpdateWidget(WidgetContext widgetContext)
    {
        try
        {
            DateTimeOffset now = DateTimeOffset.Now;
            CountdownPreferences preferences = ReadPreferences();
            CountdownState state = ApplyPreferences(ReadState(), preferences, now);
            CountdownWidgetSize size = ToCountdownWidgetSize(widgetContext.Size);
            WidgetUpdateRequestOptions options = new(widgetContext.Id)
            {
                Template = CountdownWidgetContent.BuildTemplateJson(state, size),
                Data = CountdownWidgetContent.BuildDataJson(state, now, CreateDisplayText(), size, preferences),
                CustomState = state.SelectedCountdownId
            };

            WidgetManager.GetDefault().UpdateWidget(options);
        }
        catch
        {
        }
    }

    private static CountdownWidgetSize ToCountdownWidgetSize(WidgetSize size)
    {
        return size switch
        {
            WidgetSize.Small => CountdownWidgetSize.Small,
            WidgetSize.Medium => CountdownWidgetSize.Medium,
            _ => CountdownWidgetSize.Large
        };
    }

    private static CountdownState ReadState()
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        IReadOnlyList<CountdownItem> countdowns = CountdownStateJson.DeserializeCountdowns(localSettings.Values[CountdownSettingsKeys.Countdowns] as string);
        string selectedCountdownId = localSettings.Values[CountdownSettingsKeys.SelectedCountdownId] as string ?? string.Empty;
        if (countdowns.Count > 0)
        {
            return new CountdownState(countdowns, selectedCountdownId, tileEnabled: false, toastEnabled: false);
        }

        string title = localSettings.Values[CountdownSettingsKeys.Title] as string ?? string.Empty;
        string? targetTimeZoneId = localSettings.Values[CountdownSettingsKeys.TargetTimeZoneId] as string;
        DateTimeOffset targetDate = CountdownLogic.ReadDateValue(
            localSettings.Values[CountdownSettingsKeys.TargetDate],
            CountdownTimeZone.CreateDefaultTargetDate(DateTimeOffset.Now));
        if (string.IsNullOrWhiteSpace(targetTimeZoneId))
        {
            targetDate = CountdownTimeZone.MigrateLegacyDateOnlyTarget(targetDate, targetTimeZoneId);
        }

        return new CountdownState(title, targetDate, tileEnabled: false, toastEnabled: false, targetTimeZoneId);
    }

    private static CountdownPreferences ReadPreferences()
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        CountdownHourDisplayMode hourDisplayMode = ReadHourDisplayMode(localSettings) ??
            (ReadBool(localSettings, CountdownSettingsKeys.ShowHoursWithinThreshold)
                ? CountdownHourDisplayMode.NearDeadline
                : CountdownHourDisplayMode.DaysOnly);

        return new CountdownPreferences(
            autoDeleteExpiredCountdowns: ReadBool(localSettings, CountdownSettingsKeys.AutoDeleteExpiredCountdowns),
            showHoursWithinThreshold: hourDisplayMode == CountdownHourDisplayMode.NearDeadline,
            hoursDisplayThresholdDays: ReadInt(localSettings, CountdownSettingsKeys.HoursDisplayThresholdDays) ??
                CountdownPreferences.DefaultHoursDisplayThresholdDays,
            hourDisplayMode: hourDisplayMode);
    }

    private static CountdownState ApplyPreferences(CountdownState state, CountdownPreferences preferences, DateTimeOffset now)
    {
        return preferences.AutoDeleteExpiredCountdowns
            ? state.RemoveExpiredCountdowns(now)
            : state;
    }

    private static CountdownDisplayText CreateDisplayText()
    {
        return new CountdownDisplayText(
            GetString("AppName"),
            GetString("DaysLeftOneText"),
            GetString("DaysLeftManyText"),
            GetString("DueTodayText"),
            GetString("HoursLeftOneText"),
            GetString("HoursLeftManyText"),
            GetString("DaysOverdueOneText"),
            GetString("DaysOverdueManyText"),
            GetString("DaysHoursLeftOneDayOneHourText"),
            GetString("DaysHoursLeftOneDayManyHoursText"),
            GetString("DaysHoursLeftManyDaysOneHourText"),
            GetString("DaysHoursLeftManyDaysManyHoursText"));
    }

    private static bool ReadBool(ApplicationDataContainer localSettings, string key, bool fallback = false)
    {
        return localSettings.Values[key] is bool value ? value : fallback;
    }

    private static int? ReadInt(ApplicationDataContainer localSettings, string key)
    {
        object? value = localSettings.Values[key];
        return value switch
        {
            int intValue => intValue,
            long longValue => (int)Math.Clamp(longValue, int.MinValue, int.MaxValue),
            double doubleValue when double.IsFinite(doubleValue) => (int)Math.Round(doubleValue),
            float floatValue when double.IsFinite(floatValue) => (int)Math.Round(floatValue),
            string text when int.TryParse(text, out int parsedValue) => parsedValue,
            _ => null
        };
    }

    private static CountdownHourDisplayMode? ReadHourDisplayMode(ApplicationDataContainer localSettings)
    {
        return localSettings.Values[CountdownSettingsKeys.HourDisplayMode] is string text &&
            Enum.TryParse(text, ignoreCase: true, out CountdownHourDisplayMode mode)
                ? mode
                : null;
    }

    private static string GetString(string key)
    {
        try
        {
            return new ResourceLoader().GetString(key);
        }
        catch
        {
            return string.Empty;
        }
    }
}
