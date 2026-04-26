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
            CountdownState state = ReadState();
            CountdownWidgetSize size = ToCountdownWidgetSize(widgetContext.Size);
            WidgetUpdateRequestOptions options = new(widgetContext.Id)
            {
                Template = CountdownWidgetContent.BuildTemplateJson(state, size),
                Data = CountdownWidgetContent.BuildDataJson(state, DateTimeOffset.Now, CreateDisplayText(), size),
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
        DateTimeOffset targetDate = CountdownLogic.ReadDateValue(localSettings.Values[CountdownSettingsKeys.TargetDate], DateTimeOffset.Now);

        return new CountdownState(title, targetDate, tileEnabled: false, toastEnabled: false);
    }

    private static CountdownDisplayText CreateDisplayText()
    {
        return new CountdownDisplayText(
            GetString("AppName"),
            GetString("DaysLeftOneText"),
            GetString("DaysLeftManyText"));
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
